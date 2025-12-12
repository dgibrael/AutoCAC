using AutoCAC;
using AutoCAC.Extensions;
using AutoCAC.Utilities;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.JSInterop;
using Radzen;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

public class RPMSService : IDisposable
{
    // Updated record with both OnEnter and OnExit
    public record RPMSMode(
        string Name,
        bool SignedIn,
        Action<RPMSService, string> ReceiveComplete,
        Action<RPMSService, string> Receiving,
        Action<RPMSService> OnEnter
    )
    {
        public RPMSMode(
            string Name,
            bool SignedIn
        ) : this(Name, SignedIn, (_, _) => { }, (s, data) => s.Output.SetEchoed(data), (_) => { }) { }

        public RPMSMode(
            string Name,
            bool SignedIn,
            Action<RPMSService, string> ReceiveComplete
        ) : this(Name, SignedIn, ReceiveComplete, (s, data) => s.Output.SetEchoed(data), (_) => { }) { }

        public RPMSMode(
            string Name,
            bool SignedIn,
            Action<RPMSService, string> ReceiveComplete,
            Action<RPMSService, string> Receiving
        ) : this(Name, SignedIn, ReceiveComplete, Receiving, (_) => { }) { }
    }

    public static class Modes
    {
        public static readonly RPMSMode Disconnected = new(
            Name: "Diconnected",
            SignedIn: false,
            ReceiveComplete: (s, data) =>
            {
                if (data.Contains("ACCESS CODE"))
                {
                    s.SetMode(Modes.Access);
                }
            },
            Receiving: (s, data) => s.Output.SetEchoed(data)
        );       
        public static readonly RPMSMode DefaultInput = new (
            Name: "DefaultInput",
            SignedIn: true,
            ReceiveComplete: (_, _) => { },
            Receiving: (s, data) => s.Output.SetEchoed(data)
        );
        public static readonly RPMSMode DefaultReceive = new(
            Name: "DefaultReceive",
            SignedIn: true,
            ReceiveComplete: (s, data) =>
            {
                if (data.Contains("\x1B[?7l") && data.Contains("\x1B[2") && !data.Contains("\x1B" + "7"))
                {
                    s.SetMode(Modes.ScrollWrite);
                }
                else if (data.Contains(s.EndOfFeedStr))
                {
                    //if (s.Output.Prompt().Contains("DEVICE:"))
                    //{
                    //    s.SetMode(Modes.ReportPrompt);
                    //}
                    //else
                    //{
                    //    s.SetMode(Modes.DefaultInput);
                    //}
                    s.SetMode(Modes.DefaultInput);
                }
                else if (data.Contains("\r\n\r\n\r\nLogged out at "))
                {
                    s.SetMode(Modes.Disconnected);
                }
                else
                {
                    s.SendEndOfFeed();
                }
            },
            Receiving: (s, data) =>
            {
                s.Output.Append(data);
            },
            OnEnter: (s) => s.Output.ClearBuffer()
        );
        public static readonly RPMSMode Access = new(
            Name: "Access",
            SignedIn: false,
            ReceiveComplete: (s, data) => 
            {
                if (data.Contains("VERIFY CODE")) s.SetMode(Modes.Verify);
            },
            Receiving: (s, data) => s.Output.SetEchoed(data)
        );
        public static readonly RPMSMode Verify = new(
            Name: "Verify",
            SignedIn: false,
            ReceiveComplete: (s, data) => 
            {
                if (!data.Contains("verify code", StringComparison.OrdinalIgnoreCase) &&
                    !data.Contains("that I have it right:") &&
                    !data.Contains('*'))
                {
                    if (data.Contains("Option:"))
                    {
                        s.SetMode(Modes.DefaultInput);
                    }
                    else if (data.Contains("ACCESS CODE"))
                    {
                        s.SetMode(Modes.Access);
                    }
                    else
                    {
                        s.SetMode(Modes.DefaultInput);
                    }
                }
            },
            Receiving: (s, data) => s.Output.SetEchoed(data)
        );
        public static readonly RPMSMode ScrollWrite = new(
            Name: "ScrollWrite",
            SignedIn: true,
            ReceiveComplete: (_, _) => { },
            Receiving: (s, data) => s.Output.SetEchoed(data)
        );
        public static readonly RPMSMode Report = new(
            Name: "Report",
            SignedIn: true,
            ReceiveComplete: (s, data) =>
            {
                string lastLine = data.LastLine();
                if (lastLine.Contains(s.EndOfFeedStr))
                {
                    if (string.IsNullOrEmpty(lastLine.Replace("\x07","").Replace(s.EndOfFeedStr, "").Replace("\b", "").Trim()))
                    {
                        s.Send();
                    }
                    else
                    {
                        s.SetMode(Modes.DefaultInput);
                    }
                }
            },
            Receiving: (s, data) => s.Output.Append(data)
        );
        public static readonly RPMSMode ReportPrompt = new(
            Name: "ReportPrompt",
            SignedIn: true,
            ReceiveComplete: (s, data) => { },
            Receiving: (s, data) => { },
            OnEnter: (s) => s.Output.ClearBuffer()
        );
    }

    private readonly IJSRuntime _js;
    public ShellOutput Output;
    public RPMSService(IJSRuntime js)
    {
        _js = js;
        Output = new ShellOutput(_js);
    }

    private SshClient _client;
    private ShellStream _stream;
    public ShellStream Stream => _stream;

    public RPMSMode CurrentMode { get; private set; } = Modes.Disconnected;
    public RPMSMode PreviousMode { get; private set; } = Modes.Disconnected;
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

    public void SetMode(RPMSMode newMode)
    {
        if (newMode == CurrentMode)
            return;
        PreviousMode = CurrentMode;
        CurrentMode = newMode;
        _modeChangedTcs?.TrySetResult(true); // Signal any waiter
        newMode.OnEnter(this);
        ModeChanged?.Invoke();
    }

    public string EndOfFeedStr = "\xFF\b";

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

    public void StartListening()
    {
        _listenCts = new CancellationTokenSource();
        Task.Run(() => ListenLoop(_listenCts.Token));
    }

    public void StopListening()
    {
        _listenCts?.Cancel();
    }

    public bool IsInMode(RPMSMode mode) =>
        CurrentMode == mode;

    public bool IsInMode(params RPMSMode[] modes) =>
        modes.Contains(CurrentMode);

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
                    CurrentMode.Receiving(this, data);
                }
            }
            else if (data != null)
            {
                CurrentMode.ReceiveComplete(this, data);
                data = null;
            }
            else
            {
                await Task.Delay(10, token);
            }
        }
    }


    public void WriteToXterm(string message)
    {
        _ = _js.WriteToXtermAsync(message);
    }

    public void ClearHistory()
    {
        _ = _js.ClearXtermAsync();
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
        _stream.Write(command + "\r");
        SendEndOfFeed();
        // Poll with delay
        _modeChangedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        SetMode(Modes.DefaultReceive);

        while (IsInMode(Modes.DefaultReceive))
        {
            await _modeChangedTcs.Task;
            // Reset for next mode check if still in the same mode
            if (IsInMode(Modes.DefaultReceive))
                _modeChangedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public async Task SendAsync(params string[] commands)
    {
        foreach (var command in commands)
        {
            await SendAsync(command);
        }
    }

    public async Task GoToMenu(string menu = null, int attempts = 30)
    {
        for (int i = 0; i < attempts; i++)
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
                throw new Exception("Reset verify code in RPMS");

            if (curPrompt.Contains("return", StringComparison.OrdinalIgnoreCase) ||
                curPrompt.Contains("do you wish to resume", StringComparison.OrdinalIgnoreCase))
            {
                await SendAsync();
            }
            else if (curPrompt.Contains("Select DIVISION", StringComparison.OrdinalIgnoreCase))
            {
                await SendAsync(" ");
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
                await Task.Delay(200); // now async
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
        SetMode(Modes.Report);
        while (IsInMode(Modes.Report))
        {
            await Task.Delay(10); // let other code run
        }
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

    public async Task<int?> UpdateDbTbl(string tableName)
    {
        string menu = "";
        switch (tableName)
        {
            case ("OrderDialog"):
                menu = "Order Dialog Update App";
                break;
        }
        await GoToMenu(menu);
        await SendAsync();

        var _outputLst = Output.Buffered.Split("\r\n").ToList();
        var taskLine = _outputLst.FirstOrDefault(line => line.StartsWith("Task number:"));
        if (taskLine != null && int.TryParse(taskLine.Split(":").Last().Trim(), out int taskNumber))
        {
            return taskNumber;
        }
        throw new RPMSException("Could not retreive task number");
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
