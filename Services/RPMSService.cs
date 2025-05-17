using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Linq;
using AutoCAC;

public enum RPMSMode
{
    Disconnected,
    Access,
    Verify,
    DefaultInput,
    ScrollWrite,
    ScrollRead,
    DefaultReceive,
    Report
}

public class RPMSService : IDisposable
{
    private readonly IJSRuntime _js;
    public RPMSService(IJSRuntime js)
    {
        _js = js;
    }

    private SshClient _client;
    private ShellStream _stream;
    public ShellStream Stream => _stream;

    private RPMSMode _currentMode = RPMSMode.Disconnected;
    public RPMSMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                Console.WriteLine(value);
                _currentMode = value;
                switch (value)
                {
                    case (RPMSMode.Disconnected):
                        StopListening();
                        break;
                    case (RPMSMode.Report):
                        EnableLogging = true;
                        break;
                }
            }
        }
    }

    public bool EnableLogging { get; set; } = false;
    private string _lastReceivedRaw = string.Empty;
    public string LastReceivedRaw
    {
        get => _lastReceivedRaw;
        set
        {
            if (value!=EndOfFeedStr && !IsInMode(RPMSMode.DefaultInput, RPMSMode.ScrollWrite, RPMSMode.Access))
            {
                _lastReceivedRaw = value;
            }

            // Always render it to the terminal
            _ = _js.InvokeVoidAsync("writeRPMSXterm", value, EnableLogging);
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

    public bool IsInMode(params RPMSMode[] modes)
    {
        return modes.Contains(CurrentMode);
    }
    public bool SignedIn => !IsInMode(RPMSMode.Disconnected, RPMSMode.Access, RPMSMode.Verify);

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
                HandleData(data);
                data = null;
            }
            else
            {
                await Task.Delay(10, token);
            }
        }
        if (!_client.IsConnected || !_stream.CanRead)
        {
            CurrentMode = RPMSMode.Disconnected;
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
        if (CurrentMode != RPMSMode.DefaultInput) throw new RPMSException($"Must be in write mode. Currently in {CurrentMode}");
        _stream.Write(command + "\r");
        SendEndOfFeed();
        CurrentMode = RPMSMode.DefaultReceive;
        // Poll with delay
        while (CurrentMode == RPMSMode.DefaultReceive)
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

    public bool CheckSessionActive(bool throwError = true)
    {

        if (_stream?.DataAvailable == true)
        {
            var data = _stream.Read();
            LastReceivedRaw = data;

            if (data.Contains("ACCESS CODE:") || data.Contains("Logged out"))
            {
                if (throwError) throw new InvalidOperationException("Logged out of RPMS.");
                return false;
            }
        }
        return true;
    }

    private void HandleData(string data)
    {
        switch (CurrentMode)
        {
            case RPMSMode.Access:
                if (data.Contains("VERIFY CODE")) CurrentMode = RPMSMode.Verify;
                break;
            case RPMSMode.Verify:
                if (data.Contains("verify code", StringComparison.OrdinalIgnoreCase) || 
                    data.Contains("that I have it right:") || data.Contains('*'))
                {
                    
                }
                else if (data.Contains("Option:"))
                {
                    CurrentMode = RPMSMode.DefaultInput;
                }
                else if (data.Contains("ACCESS CODE"))
                {
                    CurrentMode = RPMSMode.Access;
                }
                else
                {
                    CurrentMode = RPMSMode.DefaultInput;
                }
                break;
            case RPMSMode.DefaultReceive:
                if (data.Contains("\x1B[?7l"))
                {
                    if (data.Contains("\x1B7"))
                        CurrentMode = RPMSMode.ScrollRead;
                    else if (data.Contains("\x1B[2"))
                        CurrentMode = RPMSMode.ScrollWrite;
                }
                else if (data.Contains(EndOfFeedStr))
                {
                    if (CurrentPrompt.Contains("DEVICE:"))
                    {
                        SendRaw("0;512;999999999999\r");
                        CurrentMode = RPMSMode.Report;
                    }
                    else
                    {
                        CurrentMode = RPMSMode.DefaultInput;
                    }
                }
                break;
            case RPMSMode.ScrollRead:
                if (data.Contains("\x1B[?7l") && !data.Contains("\x1B[?25"))
                {
                    CurrentMode = RPMSMode.DefaultReceive;
                    SendEndOfFeed();
                }
                break;
            case RPMSMode.ScrollWrite:
                if (data.Contains("\x1B[?7h"))
                {
                    CurrentMode = RPMSMode.DefaultReceive;
                    SendEndOfFeed();
                }
                break;
            case RPMSMode.Disconnected:
                if (data.Contains("ACCESS CODE"))
                {
                    CurrentMode = RPMSMode.Access;
                }
                break;
            case RPMSMode.Report:
                if (data.Contains("\x07"))
                {
                    if (CurrentPrompt.Trim() == "\x07")
                    {
                        Send();
                    }
                    else
                    {
                        _ = _js.InvokeVoidAsync("downloadRPMSLog");
                        EnableLogging = false;
                        CurrentMode = RPMSMode.DefaultInput;
                    }
                }
                break;
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
