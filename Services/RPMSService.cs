using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Text;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

public class RPMSService : IDisposable
{
    private SshClient _client;
    private ShellStream _stream;
    public ShellStream Stream => _stream;
    private bool _signedIn;
    public bool SignedIn
    {
        get => _client?.IsConnected == true && _signedIn;

        set
        {
            if (!value && _signedIn)
            {
                _signedIn = false;
                Close(); // Ensures proper cleanup
            }
            else if (value)
            {
                // ✅ Optional: if you want to enforce .IsConnected check here too
                if (_client?.IsConnected != true)
                    throw new InvalidOperationException("Cannot mark as signed in if SSH client is not connected.");

                _signedIn = true;
            }
        }
    }

    private string _lastReceivedRaw;
    public string LastReceivedRaw
    {
        get => _lastReceivedRaw;
        set
        {
            _lastReceivedRaw = value?.TrimEnd().TrimEnd(EndOfFeedStr.ToCharArray()).TrimEnd();
            AddToHistory(_lastReceivedRaw);
        }
    }
    public string EndOfFeedStr { get; set; } = ((char)255).ToString();
    public RPMSService()
    {
    }

    public void OpenConnection()
    {
        _client = new SshClient("CHC-RPMS", "m", "");
        _client.Connect();

        var terminalModes = new Dictionary<TerminalModes, uint>();
        _stream = _client.CreateShellStream("vt100", 80, 24, 0, 0, 4096, terminalModes);

        WaitFor("ACCESS CODE");
    }

    public List<string> ReceivedHistory { get; private set; } = new();

    private int _maxCharCount = 100000;
    private void AddToHistory(string message)
    {
        ReceivedHistory.Add(message);

        int totalChars = ReceivedHistory.Sum(m => m.Length);

        while (totalChars > _maxCharCount && ReceivedHistory.Count > 0)
        {
            totalChars -= ReceivedHistory[0].Length;
            ReceivedHistory.RemoveAt(0);
        }
    }

    public void ClearHistory()
    {
        ReceivedHistory.Clear();
    }

    public void Login(Span<char> accessCode, Span<char> verifyCode)
    {
        if (_client?.IsConnected == true)
        {
            Close();
        }
        OpenConnection();
        SendSecure(accessCode);
        SendSecure(verifyCode);

        for (int i = 0; i < 20; i++)
        {
            int result = WaitForAny(new[] {
            "ACCESS CODE:",
            "Please enter your CURRENT verify code",
            "ption:",
            "Select DIVISION",
            "to stop:",
            "return",
            "Return",
            "RETURN",
            "//"
            });

            switch (result)
            {
                case 0:
                    throw new Exception("Incorrect access or verify code");
                case 1:
                    throw new Exception("Reset verify code in RPMS");
                case 2:
                    SignedIn = true;
                    return;
                case 3:
                    SendRaw(" ");
                    break;
                default:
                    SendRaw(""); // Press ENTER
                    break;
            }
        }

        throw new Exception("Unknown Menu Location. Check RPMS screen.");
    }

    private void SendSecure(Span<char> input)
    {
        try
        {
            _stream.Write(new string(input) + "\r");
        }
        finally
        {
            input.Clear();
        }
    }

    public void SendRaw(string command = "")
    {
        _stream.Write(command + "\r");
    }

    public List<string> GetReceivedLines()
    {
        return LastReceivedRaw
            ?.Split("\r\n")
            .ToList()
            ?? new List<string>();
    }

    public string CurrentPrompt => GetReceivedLines().LastOrDefault() ?? string.Empty;

    private void WaitFor(string waitFor)
    {
        var sb = new StringBuilder();
        while (true)
        {
            if (_stream.DataAvailable)
            {
                var data = _stream.Read();
                sb.AppendLine(data);
                if (data.Contains(waitFor))
                {
                    LastReceivedRaw = sb.ToString();
                    break;
                }
            }
        }
    }

    private int WaitForAny(string[] options)
    {
        var sb = new StringBuilder();
        while (true)
        {
            if (_stream.DataAvailable)
            {
                var data = _stream.Read();
                sb.Append(data);
                for (int i = 0; i < options.Length; i++)
                {
                    if (data.Contains(options[i]))
                    {
                        LastReceivedRaw = sb.ToString();
                        return i;
                    }
                }
            }
        }
    }

    public bool CheckSessionActive(bool throwError = false)
    {
        if (SignedIn == false)
        {
            if (throwError) throw new InvalidOperationException("Logged out of RPMS.");
            return false;
        }

        if (_stream?.DataAvailable == true)
        {
            var data = _stream.Read();
            LastReceivedRaw = data;

            if (data.Contains("ACCESS CODE:") || data.Contains("Logged out"))
            {
                SignedIn = false;
                if (throwError) throw new InvalidOperationException("Logged out of RPMS.");
                return false;
            }
        }
        return true;
    }

    public void Close()
    {
        if (_stream != null)
        {
            _stream.Dispose();
            _stream = null;
        }

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
