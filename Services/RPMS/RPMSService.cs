using AutoCAC;
using AutoCAC.Exceptions;
using AutoCAC.Extensions;
using AutoCAC.Options;
using AutoCAC.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Radzen;
using Renci.SshNet;
using Renci.SshNet.Common;
namespace AutoCAC.Services;

public enum RPMSMode
{
    Disconnected,
    DefaultInput,
    DefaultReceive,
    Access,
    Verify,
    ScrollWrite,
    Report,
    ReportPrompt
}

public static class RPMSModeExtensions
{
    extension(RPMSMode mode)
    {
        public bool SignedIn => mode switch
        {
            RPMSMode.Disconnected or RPMSMode.Access
            or RPMSMode.Verify => false,
            _ => true
        };
    }
}

public class RPMSService : IDisposable
{
    private readonly IJSRuntime _js;
    public ShellOutput Output;
    private readonly RpmsOptions _options;
    public RPMSService(IJSRuntime js, IOptions<RpmsOptions> options)
    {
        _js = js;
        Output = new ShellOutput(_js);
        _options = options.Value;
    }

    private SshClient _client;
    private ShellStream _stream;
    public ShellStream Stream => _stream;

    public RPMSMode CurrentMode { get; private set; } = RPMSMode.Disconnected;
    public RPMSMode PreviousMode { get; private set; } = RPMSMode.Disconnected;

    private TaskCompletionSource<bool> _modeChangedTcs;

    public bool JustSignedIn => !PreviousMode.SignedIn && CurrentMode.SignedIn;

    public event Action ModeChanged;
    private readonly List<Action> _modeChangedSubscribers = new();

    public void SubscribeToModeChanged(Action handler)
    {
        UnSubscribeToModeChanged(handler); // idempotent
        _modeChangedSubscribers.Add(handler);
        ModeChanged += handler;
    }

    public void UnSubscribeToModeChanged(Action handler)
    {
        ModeChanged -= handler;
        _modeChangedSubscribers.Remove(handler);
    }

    public void ClearAllModeChangedSubscriptions()
    {
        foreach (var handler in _modeChangedSubscribers.ToList())
        {
            ModeChanged -= handler;
        }
        _modeChangedSubscribers.Clear();
    }

    public async Task WaitUntilNotModeAsync(RPMSMode mode, CancellationToken cancellationToken = default)
    {
        // If we already are NOT in that mode, return immediately.
        if (CurrentMode != mode)
            return;

        var startVersion = Interlocked.Read(ref _modeVersion);

        while (CurrentMode == mode)
        {
            // Wait for any mode change since startVersion (version-safe).
            while (Interlocked.Read(ref _modeVersion) == startVersion)
                await WaitForNextModeChangeAsync(cancellationToken);

            startVersion = Interlocked.Read(ref _modeVersion);
        }
    }

    public async Task WaitUntilModeAsync(RPMSMode? targetMode = null, CancellationToken cancellationToken = default)
    {
        // Fast-path if we already satisfy the condition.
        if (targetMode.HasValue)
        {
            if (CurrentMode == targetMode.Value)
                return;
        }

        // Capture version BEFORE we potentially start waiting.
        var startVersion = Interlocked.Read(ref _modeVersion);

        while (true)
        {
            if (!targetMode.HasValue)
            {
                // Wait for *any* mode change since startVersion.
                while (Interlocked.Read(ref _modeVersion) == startVersion)
                    await WaitForNextModeChangeAsync(cancellationToken);

                return;
            }

            // Wait-until-target: loop until mode matches.
            if (CurrentMode == targetMode.Value)
                return;

            // Wait for at least one change (avoid busy looping).
            while (Interlocked.Read(ref _modeVersion) == startVersion)
                await WaitForNextModeChangeAsync(cancellationToken);

            // Update baseline and loop again if not at target yet.
            startVersion = Interlocked.Read(ref _modeVersion);
        }
    }

    private Task WaitForNextModeChangeAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler()
        {
            ModeChanged -= Handler;
            tcs.TrySetResult(true);
        }

        ModeChanged += Handler;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                ModeChanged -= Handler;
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        return tcs.Task;
    }
    private long _modeVersion;
    public void SetMode(RPMSMode newMode)
    {
        if (newMode == CurrentMode)
            return;
        PreviousMode = CurrentMode;
        CurrentMode = newMode;
        _modeChangedTcs?.TrySetResult(true); // Signal any waiter
        if (CurrentMode is (RPMSMode.DefaultReceive or RPMSMode.ReportPrompt))
            Output.ClearBuffer();
        Interlocked.Increment(ref _modeVersion);
        ModeChanged?.Invoke();
    }

    public string EndOfFeedStr { get; } = "\xFF\b";

    private CancellationTokenSource _listenCts;

    public void OpenConnection()
    {
        if (_client?.IsConnected == true)
        {
            Close();
        }
        _client = new SshClient("CHC-RPMS", "m", "");
        _client.Connect();

        var terminalModes = new Dictionary<TerminalModes, uint>();
        _stream = _client.CreateShellStream("xterm", 120, 24, 0, 0, 4096, terminalModes);
        StartListening();
    }

    private Task _listenTask;

    void StartListening()
    {
        if (_listenTask != null && !_listenTask.IsCompleted)
            return;

        _listenCts?.Cancel();
        _listenCts?.Dispose();
        _listenCts = new CancellationTokenSource();

        _listenTask = Task.Run(() => ListenLoop(_listenCts.Token));
    }

    void StopListening()
    {
        _listenCts?.Cancel();
    }

    public bool IsInMode(params RPMSMode[] modes) =>
        modes.Contains(CurrentMode);

    private void Receiving(string data)
    {
        switch (CurrentMode)
        {
            case RPMSMode.DefaultReceive:
            case RPMSMode.Report:
            case RPMSMode.Verify:
                Output.Append(data);
                break;
            default:
                Output.SetEchoed(data);
                break;
        }
    }
    private void ReceiveComplete(string data)
    {
        switch (CurrentMode)
        {
            case RPMSMode.Disconnected when data.Contains("ACCESS CODE"):
                SetMode(RPMSMode.Access);
                SendRaw(_options.Access);
                SendRaw("\r");
                break;

            case RPMSMode.DefaultReceive:
                if (data.Contains("\x1B[?7l") && data.Contains("\x1B[2") && !data.Contains("\x1B" + "7"))
                {
                    SetMode(RPMSMode.ScrollWrite);
                    break;
                }
                if (data.Contains(EndOfFeedStr))
                {
                    if (Output.Prompt().Contains("DEVICE:"))
                    {
                        SetMode(RPMSMode.ReportPrompt);
                    }
                    else
                    {
                        SetMode(RPMSMode.DefaultInput);
                    }
                    break;
                }
                if (data.Contains("\r\n\r\n\r\nLogged out at "))
                {
                    SetMode(RPMSMode.Disconnected);
                    break;
                }
                SendEndOfFeed();
                break;
            case RPMSMode.Access when data.Contains("VERIFY CODE"):
                SetMode(RPMSMode.Verify);
                SendRaw(_options.Verify);
                SendRaw("\r");
                break;
            case RPMSMode.Verify:
                if (data.Contains("verify code", StringComparison.OrdinalIgnoreCase) ||
                    data.Contains("that I have it right:"))
                    break;
                if (data.Contains("Option:"))
                {
                    SetMode(RPMSMode.DefaultInput);
                    break;
                }
                if (data.Contains("ACCESS CODE:"))
                {
                    SetMode(RPMSMode.Access);
                    break;
                }
                SetMode(RPMSMode.DefaultInput);
                break;
            case RPMSMode.Report:
                string lastLine = data.LastLine();
                if (lastLine.Contains(EndOfFeedStr))
                {
                    if (string.IsNullOrEmpty(lastLine.Replace("\x07", "").Replace(EndOfFeedStr, "").Replace("\b", "").Trim()))
                    {
                        Send();
                        break;
                    }
                    SetMode(RPMSMode.DefaultInput);
                    break;
                }
                break;
        }
    }

    private async Task ListenLoop(CancellationToken token)
    {
        string data = null;

        while (!token.IsCancellationRequested && _stream?.CanRead == true)
        {
            if (_stream.DataAvailable)
            {
                data = _stream.Read();

                if (data!=null)
                {
                    Receiving(data);
                }
            }
            else if (data != null)
            {
                ReceiveComplete(data);
                data = null;
            }
            else
            {
                await Task.Delay(10, token);
            }
        }
    }

    public void SendRaw(string command = "")
    {
        _stream.Write(command);
    }

    public void Send(string command = "")
    {
        _stream.Write(command + "\r");
        SendEndOfFeed();
    }

    public async Task SendAsync(string command = "")
    {
        _stream.Write(command);
        _stream.Write("\r");
        SendEndOfFeed();
        SetMode(RPMSMode.DefaultReceive);
        await WaitUntilNotModeAsync(RPMSMode.DefaultReceive);
    }

    public async Task SendAsync(params string[] commands)
    {
        foreach (var command in commands)
        {
            await SendAsync(command);
        }
    }

    public async Task Login()
    {
        SetMode(RPMSMode.Disconnected);
        OpenConnection();
        SendRaw("\r");
        while (!CurrentMode.SignedIn)
            await WaitUntilModeAsync();
    }

    public async Task GoToMenu(string menu = null, int attempts = 30)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                var curPrompt = Output.Prompt();
                if (curPrompt.Contains("AutoCAC App Main Menu Option:"))
                {
                    if (menu != null)
                    {
                        await SendAsync(menu);
                    }
                    return;
                }

                if (curPrompt.Contains("Please enter your CURRENT verify code", StringComparison.OrdinalIgnoreCase))
                    throw new RPMSException("Reset verify code in RPMS");

                if (curPrompt.Contains("return", StringComparison.OrdinalIgnoreCase) ||
                    curPrompt.Contains("do you wish to resume", StringComparison.OrdinalIgnoreCase))
                {
                    await SendAsync();
                }
                else if (curPrompt.Contains("Select DIVISION", StringComparison.OrdinalIgnoreCase))
                {
                    await SendAsync(_options.Division);
                }
                else if (curPrompt.Contains("to stop", StringComparison.OrdinalIgnoreCase) ||
                            curPrompt.Contains("halt?", StringComparison.OrdinalIgnoreCase))
                {
                    await SendAsync("^");
                }
                else if (curPrompt.Contains("Option:"))
                {
                    if (curPrompt.Contains("AutoCAC App Main Menu"))
                    {
                        if (menu != null)
                        {
                            await SendAsync(menu);
                        }
                        return;
                    }
                    await SendAsync("^AutoCAC App Main Menu");
                }
                else
                {
                    await SendAsync("^");
                }

                if (attempts - i <= 3)
                {
                    await Task.Delay(200);
                }
            }
            catch (ObjectDisposedException)
            {
                await Login();
            }
        }

        throw new Exception("Could not reach main menu. Ask IRM/CAC/Informatics to assign secondary menu option: AutoCAC App Main Menu");
    }

    public string CheckPrompt(StringComparison comparison, params string[] expectedPrompts)
    {
        string prompt = Output.Prompt();

        foreach (var expected in expectedPrompts)
        {
            if (prompt.Contains(expected, comparison))
            {
                return expected;
            }
        }
        return null;
    }
    public string CheckPrompt(params string[] expectedPrompts) => CheckPrompt(StringComparison.Ordinal, expectedPrompts);

    public void CheckPromptAndThrow(string expectedPrompt, StringComparison comparison = StringComparison.Ordinal)
    {
        string prompt = Output.Prompt();
        if (!prompt.Contains(expectedPrompt, comparison))
        {
            throw new RPMSException();
        }
    }

    /// <summary>
    /// Repeatedly handles the RPMS prompt and runs the corresponding handler action.
    /// Each handler returns a boolean: true = exit early, false = continue.
    /// </summary>
    /// <param name="maxLoops">Maximum number of prompt iterations.</param>
    /// <param name="handlers">Dictionary of prompt string to handler function.</param>
    /// <returns>Task representing the async operation.</returns>
    /// <example>
    /// await Layout.PromptLoopAsync(new Dictionary<string, Func<Task<bool>>>
    /// {
    ///     ["Yes or No"] = async () => { await RPMS.SendAsync("YES"); return true; },
    ///     ["Is this a match "] = async () => { await RPMS.SendAsync("YES"); return true; },
    ///     ["Press Return"] = async () => { await RPMS.SendAsync(); return false; },
    ///     ["Enter RETURN to continue"] = async () => { await RPMS.SendAsync(); return false; },
    ///     [" edit "] = async () => { await Layout.EnterUntil("Enter your choice(s) separated by commas"); return false; },
    ///     ["osages"] = async () => { await Layout.EnterUntil("Enter your choice(s) separated by commas"); return false; }
    /// }, 20);
    /// </example>
    public async Task PromptLoopAsync(Dictionary<string, Func<Task<bool>>> handlers, int maxLoops = 30)
    {
        for (int i = 0; i < maxLoops; i++)
        {
            var prompt = CheckPrompt(handlers.Keys.ToArray());

            if (!handlers.TryGetValue(prompt, out var handler))
                throw new RPMSException();

            bool shouldExit = await handler();
            if (shouldExit)
                return; // exits outer method
        }
        throw new RPMSException();
    }

    public async Task<string> EnterUntil(int maxTries, StringComparison comparison, params string[] prompts)
    {
        for (int i = 0; i < maxTries; i++)
        {
            var curPrompt = CheckPrompt(comparison, prompts);
            if (curPrompt != null)
            {
                return curPrompt;
            }
            await SendAsync("");
        }
        throw new RPMSException();
    }
    public async Task EnterUntil(params string[] prompts) => await EnterUntil(30, StringComparison.Ordinal, prompts);

    public async Task<string> GetReportAsStringAsync()
    {
        Output.BufferFrozen = true;
        Send("0;512;99999999999");
        SetMode(RPMSMode.Report);
        await WaitUntilNotModeAsync(RPMSMode.Report);
        string buffer = Output.Buffered;
        Output.BufferFrozen = false;
        Output.ClearBuffer();
        return buffer;
    }

    public async Task<List<string>> GetOptions(int maxLoops = 20)
    {
        await SendAsync("??");
        Output.BufferFrozen = true;
        for (int i = 0; i<maxLoops; i++)
        {
            if (!CurrentPromptContains("to stop", StringComparison.OrdinalIgnoreCase))
            {
                Output.BufferFrozen = false;
                return Output.BufferList();
            }
            await SendAsync();
        }
        Output.BufferFrozen = false;
        await SendAsync("^");
        return Output.BufferList();
    }

    public void SendEndOfFeed()
    {
        _stream.Write(EndOfFeedStr);
    }

    public bool CurrentPromptContains(string value, StringComparison comparison = StringComparison.Ordinal) => 
        Output.Buffered.LastLineContains(value, comparison: comparison);
    public string CurrentPromptContains(string[] values, StringComparison comparison = StringComparison.Ordinal) =>
        Output.Buffered.LastLineContains(values, comparison: comparison);
    public void CurrentPromptContains(params (string text, Action action, bool exitLoop)[] handlers)
    {
        var prompt = Output.Buffered.LastLineSpan();

        foreach (var (text, action, exitLoop) in handlers)
        {
            if (string.IsNullOrEmpty(text)) continue;

            if (prompt.IndexOf(text.AsSpan()) >= 0)
            {
                action?.Invoke();
                if (exitLoop)
                    break;
            }
        }
    }
    public async Task HandlePrompt(
        List<(string Prompt, Func<Task> Action)> promptActions,
        Func<Task> onNoMatch = null,
        StringComparison comparison = StringComparison.Ordinal)
    {
        string prompt = Output.Prompt();
        foreach (var (expectedPrompt, action) in promptActions)
        {
            if (prompt.Contains(expectedPrompt, comparison))
            {
                await action();
                return;
            }
        }

        if (onNoMatch is not null)
        {
            await onNoMatch();
        }
        else
        {
            throw new RPMSException();
        }
    }

    public void Close()
    {
        ClearAllModeChangedSubscriptions();
        StopListening();
        _stream?.Dispose();
        _stream = null;

        if (_client != null)
        {
            if (_client.IsConnected) _client.Disconnect();
            _client.Dispose();
            _client = null;
        }
    }

    public void Dispose()
    {
        Close();
    }
}
