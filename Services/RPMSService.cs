using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Linq;
using AutoCAC;
using AutoCAC.Extensions;
using AutoCAC.Utilities;
using Microsoft.Identity.Client;

public class RPMSService : IDisposable
{
    // Updated record with both OnEnter and OnExit
    public record RPMSMode(
        bool SignedIn,
        Action<RPMSService, string> ReceiveComplete,
        Action<RPMSService, string> Receiving,
        Action<RPMSService> OnEnter
    )
    {
        public RPMSMode(
            bool SignedIn
        ) : this(SignedIn, (_, _) => { }, (s, data) => s.Output.SetEchoed(data), (_) => { }) { }

        public RPMSMode(
            bool SignedIn,
            Action<RPMSService, string> ReceiveComplete
        ) : this(SignedIn, ReceiveComplete, (s, data) => s.Output.SetEchoed(data), (_) => { }) { }

        public RPMSMode(
            bool SignedIn,
            Action<RPMSService, string> ReceiveComplete,
            Action<RPMSService, string> Receiving
        ) : this(SignedIn, ReceiveComplete, Receiving, (_) => { }) { }
    }

    public static class Modes
    {
        public static readonly RPMSMode Disconnected = new(
            SignedIn: false,
            ReceiveComplete: (s, data) =>
            {
                if (data.Contains("ACCESS CODE"))
                {
                    s.SetMode(Modes.Access);
                }
            },
            Receiving: (s, data) => s.Output.SetEchoed(data),
            OnEnter: (_) => Console.WriteLine("Disconnected")
        );       
        public static readonly RPMSMode DefaultInput = new (
            SignedIn: true,
            ReceiveComplete: (_, _) => { },
            Receiving: (s, data) => s.Output.SetEchoed(data),
            OnEnter: (_) => Console.WriteLine("DefaultInput")
        );
        public static readonly RPMSMode DefaultReceive = new (
            SignedIn: true,
            ReceiveComplete: (s, data) => 
            {
                if (data.Contains("\x1B[?7l") && data.Contains("\x1B[2") && !data.Contains("\x1B" + "7"))
                {
                    s.SetMode(Modes.ScrollWrite);
                }
                else if (data.Contains(s.EndOfFeedStr))
                {
                    if (s.Output.Prompt().Contains("DEVICE:"))
                    {
                        s.SetMode(Modes.Report);
                    }
                    else
                    {
                        s.SetMode(Modes.DefaultInput);
                    }
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
            Receiving: (s, data) => s.Output.SetReceived(data),
            OnEnter: (_) => Console.WriteLine("DefaultReceive")
        );
        public static readonly RPMSMode Access = new(
            SignedIn: false,
            ReceiveComplete: (s, data) => 
            {
                if (data.Contains("VERIFY CODE")) s.SetMode(Modes.Verify);
            },
            Receiving: (s, data) => s.Output.SetEchoed(data),
            OnEnter: (_) => Console.WriteLine("Access")
        );
        public static readonly RPMSMode Verify = new(
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
            Receiving: (s, data) => s.Output.SetEchoed(data),
            OnEnter: (_) => Console.WriteLine("Verify")
        );
        public static readonly RPMSMode ScrollWrite = new(
            SignedIn: true,
            ReceiveComplete: (_, _) => { },
            Receiving: (s, data) => s.Output.SetEchoed(data),
            OnEnter: (_) => Console.WriteLine("ScrollWrite")
        );
        public static readonly RPMSMode Report = new(
            SignedIn: true,
            ReceiveComplete: (s, data) =>
            {
                string lastLine = data.LastLine();
                if (lastLine.Contains("\x07") || lastLine.Contains(s.EndOfFeedStr))
                {
                    if (lastLine.Trim() == "\x07")
                    {
                        s.SendRaw("\r");
                    }
                    else
                    {
                        s.SetMode(Modes.DefaultInput);
                    }
                }
                else
                {
                    s.SendEndOfFeed();
                }
            },
            Receiving: (s, data) => s.Output.Append(data),
            OnEnter: (s) =>
            {
                Console.WriteLine("Report");
                //s.SendRaw("0;512;999999999999\r");
            }
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
    public event Action ModeChanged;
    public void SetMode(RPMSMode newMode)
    {
        if (newMode == CurrentMode)
            return;
        CurrentMode = newMode;
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
        _stream = _client.CreateShellStream("xterm", 80, 24, 0, 0, 4096, terminalModes);
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
        if (!_client.IsConnected || !_stream.CanRead)
        {
            SetMode(Modes.Disconnected);
            StopListening();
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
        if (!IsInMode(Modes.DefaultInput)) throw new RPMSException($"Must be in write mode. Currently in {CurrentMode}");
        _stream.Write(command + "\r");
        SendEndOfFeed();
        SetMode(Modes.DefaultReceive);
        // Poll with delay
        while (IsInMode(Modes.DefaultReceive))
        {
            await Task.Delay(10); // let other code run
        }
    }

    public async Task<IEnumerable<T>> GetReportAsync<T>()
    {
        Console.WriteLine("running report");
        string prompt = Output.Prompt();
        if (!prompt.Contains("DEVICE", StringComparison.OrdinalIgnoreCase)) throw new RPMSException($"Must be at Device prompt. Current Prompt: {prompt}");
        _stream.Write("0;512;99999999999" + "\r");
        //SetMode(Modes.Report);
        while (IsInMode(Modes.Report))
        {
            await Task.Delay(10); // let other code run
        }
        Console.WriteLine("report complete parsing...");
        return Output.BufferToObject<T>();
    }

    public void SendEndOfFeed()
    {
        _stream.Write(EndOfFeedStr);
    }

    public bool CurrentPromptContains(string value, StringComparison comparison = StringComparison.Ordinal) => 
        Output.Received.LastLineContains(value, comparison: comparison);
    public string CurrentPromptContains(string[] values, StringComparison comparison = StringComparison.Ordinal) =>
        Output.Received.LastLineContains(values, comparison: comparison);
    public void CurrentPromptContains(params (string text, Action action, bool exitLoop)[] handlers)
    {
        var prompt = Output.Received.LastLineSpan();

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
