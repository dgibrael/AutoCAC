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
        bool InputEnabled,
        bool LoggingEnabled,
        bool FixedCursor,
        Action<RPMSService, string> HandleData,
        Action OnEnter,
        Action OnExit
    )
    {
        // Overload 1: only bools
        public RPMSMode(
            bool SignedIn,
            bool InputEnabled,
            bool LoggingEnabled,
            bool FixedCursor
        ) : this(SignedIn, InputEnabled, LoggingEnabled, FixedCursor, (_, _) => { }, () => { }, () => { }) { }

        // Overload 2: bools + HandleData
        public RPMSMode(
            bool SignedIn,
            bool InputEnabled,
            bool LoggingEnabled,
            bool FixedCursor,
            Action<RPMSService, string> HandleData
        ) : this(SignedIn, InputEnabled, LoggingEnabled, FixedCursor, HandleData, () => { }, () => { }) { }

        // Overload 3: bools + HandleData + OnEnter
        public RPMSMode(
            bool SignedIn,
            bool InputEnabled,
            bool LoggingEnabled,
            bool FixedCursor,
            Action<RPMSService, string> HandleData,
            Action OnEnter
        ) : this(SignedIn, InputEnabled, LoggingEnabled, FixedCursor, HandleData, OnEnter, () => { }) { }
    }

    public static class Modes
    {
        public static readonly RPMSMode Disconnected = new (
            SignedIn: false,
            InputEnabled: false,
            LoggingEnabled: false,
            FixedCursor: true,
            HandleData: (_, _) => { },
            OnEnter: () => Console.WriteLine("Disconnected")
        );        
        public static readonly RPMSMode DefaultInput = new (
            SignedIn: true,
            InputEnabled: true,
            LoggingEnabled: false,
            FixedCursor: true,
            HandleData: (_, _) => { },
            OnEnter: () => Console.WriteLine("DefaultInput")
        );
        public static readonly RPMSMode DefaultReceive = new (
            SignedIn: true,
            InputEnabled: false,
            LoggingEnabled: false,
            FixedCursor: true,
            HandleData: (_, _) => { },
            OnEnter: () => Console.WriteLine("DefaultReceive")
        );
        public static readonly RPMSMode Access = new(
            SignedIn: false,
            InputEnabled: true,
            LoggingEnabled: false,
            FixedCursor: true,
            HandleData: (s, data) => 
            {
                if (data.EndContains("VERIFY CODE")) s.SetMode(Modes.Verify);
            },
            OnEnter: () => Console.WriteLine("Access")
        );
        public static readonly RPMSMode Verify = new(
            SignedIn: false,
            InputEnabled: true,
            LoggingEnabled: false,
            FixedCursor: true,
            HandleData: (s, data) => 
            {
                if (!data.EndContains("verify code", StringComparison.OrdinalIgnoreCase) &&
                    !data.Contains("that I have it right:") &&
                    !data.EndContains('*'))
                {
                    if (data.EndContains("Option:", charsFromEnd: 10))
                    {
                        s.SetMode(Modes.DefaultInput);
                    }
                    else if (data.EndContains("ACCESS CODE"))
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
            InputEnabled: true,
            LoggingEnabled: false,
            FixedCursor: false,
            HandleData: (_, _) => { },
            OnEnter: () => Console.WriteLine("ScrollWrite")
        );
        public static readonly RPMSMode Report = new(
            SignedIn: true,
            InputEnabled: false,
            LoggingEnabled: true,
            FixedCursor: true,
            HandleData: (_, _) => { },
            OnEnter: () => Console.WriteLine("Report")
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

        CurrentMode.OnExit();      // Run exit logic first
        CurrentMode = newMode;
        newMode.OnEnter();         // Then run enter logic
    }


    public bool EnableLogging { get; set; } = false;
    private string _lastReceivedRaw = string.Empty;
    public string LastReceivedRaw
    {
        get => _lastReceivedRaw;
        set
        {
            if (value!=EndOfFeedStr && !CurrentMode.InputEnabled)
            {
                _lastReceivedRaw = value;
            }

            // Always render it to the terminal
            _ = _js.InvokeVoidAsync("writeRPMSXterm", value, CurrentMode.LoggingEnabled);
        }
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
                    LastReceivedRaw = data;
                }
            }
            else if (data != null)
            {
                CurrentMode.HandleData(this, data);
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
        _ = _js.InvokeVoidAsync("writeRPMSXterm", message);
    }

    public void ClearHistory()
    {
        _ = _js.InvokeVoidAsync("clearRPMSXterm");
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

    private static readonly string[] LineSeparators = new[] { "\r\n", "\n", "\r" };
    public List<string> GetReceivedLines()
    {
        return LastReceivedRaw?
            .Split(LineSeparators, StringSplitOptions.None)
            .ToList()
            ?? new List<string>();
    }

    public string CurrentPrompt => GetReceivedLines().LastOrDefault() ?? string.Empty;

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
