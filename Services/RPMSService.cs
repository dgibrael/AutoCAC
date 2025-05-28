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

public class RPMSService : IDisposable
{
    // Updated record with both OnEnter and OnExit
    public record RPMSMode(
        bool SignedIn,
        Action<RPMSService, string> ReceiveComplete,
        Action OnEnter,
        Action<RPMSService, string> Receiving
    )
    {
        public RPMSMode(
            bool SignedIn
        ) : this(SignedIn, (_, _) => { }, () => { },
            (s, data) => 
            {
                _ = s._js.WriteToXtermAsync(data);
            }) { }

        public RPMSMode(
            bool SignedIn,
            Action<RPMSService, string> ReceiveComplete
        ) : this(SignedIn, ReceiveComplete, () => { }, 
            (s, data) =>
            {
                _ = s._js.WriteToXtermAsync(data);
            }) { }

        public RPMSMode(
            bool SignedIn,
            Action<RPMSService, string> ReceiveComplete,
            Action OnEnter
        ) : this(SignedIn, ReceiveComplete, OnEnter, 
            (s, data) =>
            {
                _ = s._js.WriteToXtermAsync(data);
            }) { }
    }

    public static class Modes
    {
        public static readonly RPMSMode Disconnected = new (
            SignedIn: false,
            ReceiveComplete: (s, data) =>
            {
                if (data.Contains("ACCESS CODE"))
                {
                    s.SetMode(Modes.Access);
                }
            },
            OnEnter: () => Console.WriteLine("Disconnected")
        );        
        public static readonly RPMSMode DefaultInput = new (
            SignedIn: true,
            ReceiveComplete: (_, _) => { },
            OnEnter: () => Console.WriteLine("DefaultInput")
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
                    if (s.CurrentPrompt.Contains("DEVICE:"))
                    {
                        s.SendRaw("0;512;999999999999\r");
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
            OnEnter: () => Console.WriteLine("DefaultReceive"),
            Receiving: (s, data) =>
            {
                s.SetLastReceived(data);
                _ = s._js.WriteToXtermAsync(data, true);
            }
        );
        public static readonly RPMSMode Access = new(
            SignedIn: false,
            ReceiveComplete: (s, data) => 
            {
                if (data.Contains("VERIFY CODE")) s.SetMode(Modes.Verify);
            },
            OnEnter: () => Console.WriteLine("Access")
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
            OnEnter: () => Console.WriteLine("Verify")
        );
        public static readonly RPMSMode ScrollWrite = new(
            SignedIn: true,
            ReceiveComplete: (_, _) => { },
            OnEnter: () => Console.WriteLine("ScrollWrite")
        );
        public static readonly RPMSMode Report = new(
            SignedIn: true,
            ReceiveComplete: (s, data) =>
            {
                if (data.Contains("\x07"))
                {
                    if (s.CurrentPrompt.Trim() == "\x07")
                    {
                        s.SendRaw("\r");
                    }
                    else
                    {
                        _ = s._js.DownloadLogAsync();
                        s.SetMode(Modes.DefaultInput);
                    }
                }
            },
            OnEnter: () => Console.WriteLine("Report"),
            Receiving: (s, data) =>
            {
                _ = s._js.WriteToXtermAsync(data, true);
            }
        );
    }

    private readonly IJSRuntime _js;
    public RPMSService(IJSRuntime js)
    {
        _js = js;
    }

    private SshClient _client;
    private ShellStream _stream;
    public ShellStream Stream => _stream;

    public RPMSMode CurrentMode { get; private set; } = Modes.Disconnected;

    public void SetMode(RPMSMode newMode)
    {
        if (newMode == CurrentMode)
            return;
        CurrentMode = newMode;
        newMode.OnEnter();
    }

    public void SetLastReceived(string data)
    {
        if (data != EndOfFeedStr)
        {
            LastReceivedRaw = data;
        }
    }
    public string LastReceivedRaw { get; set; } = string.Empty;

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

    public void SendEndOfFeed()
    {
        _stream.Write(EndOfFeedStr);
    }

    public string CurrentPrompt
    {
        get
        {
            var data = LastReceivedRaw;
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            ReadOnlySpan<char> span = data.AsSpan();
            int lastIndex = span.LastIndexOfAny('\r', '\n');

            return lastIndex >= 0
                ? span[(lastIndex + 1)..].ToString()
                : data;
        }
    }

    public bool CurrentPromptContains(string value, StringComparison comparison = StringComparison.Ordinal) => 
        LastReceivedRaw.LastLineContains(value, comparison: comparison);
    public string CurrentPromptContains(string[] values, StringComparison comparison = StringComparison.Ordinal) => 
        LastReceivedRaw.LastLineContains(values, comparison: comparison);
    public void CurrentPromptContains(params (string text, Action action, bool exitLoop)[] handlers)
    {
        var prompt = LastReceivedRaw.LastLineSpan();

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
