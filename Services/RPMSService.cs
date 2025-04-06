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
    private string _lastReceivedRaw;
    public string LastReceivedRaw 
    { 
        get => _lastReceivedRaw; 
        private set
        {
            _lastReceivedRaw = value?.Trim().TrimEnd(_endOfFeedStr.ToCharArray()).Trim();
            AddToHistory(_lastReceivedRaw);
        }
    }
    private readonly string _endOfFeedStr = ((char)255).ToString();

    private bool _signedIn = false;
    public event Action OnConnectionChanged;
    private bool _wasConnected;

    public bool IsConnected
    {
        get
        {
            bool currentlyConnected = _client?.IsConnected == true && _signedIn;

            if (currentlyConnected != _wasConnected)
            {
                _wasConnected = currentlyConnected;
                OnConnectionChanged?.Invoke();
            }

            return currentlyConnected;
        }
    }

    public event Action OnDisconnected;

    public RPMSService()
    {
        _signedIn = false;
    }

    public void OpenConnection()
    {
        _client = new SshClient("CHC-RPMS", "m", "");
        _client.Connect();

        var terminalModes = new Dictionary<TerminalModes, uint>();
        _stream = _client.CreateShellStream("vt100", 255, 1000, 0, 0, 4096, terminalModes);

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
        if (_client?.IsConnected != true)
        {
            OpenConnection();
        }
        _signedIn = false;
        SendSecure(accessCode);
        SendSecure(verifyCode);

        for (int i = 0; i < 15; i++)
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
                    _signedIn = true;
                    GoToMainMenu();
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
            _stream.Write(new string(input)+"\r");
        }
        finally
        {
            input.Clear();
        }
    }

    public void Send(string command = "")
    {
        if (!IsConnected) return;
        try
        {
            SendRaw(command);
            Read();
        }
        catch
        {
            Close();
            throw;
        }
    }

    private void SendRaw(string command)
    {
        _stream.Write(command+"\r");
    }

    public int MaxReadLoops { get; set; } = 50000; // ~500 seconds at 10ms sleep
    private void Read()
    {
        _stream.Write(_endOfFeedStr);
        var sb = new StringBuilder();

        const int earlyExitCheckThreshold = 500; // ~5 seconds
        int loopCounter = 0;
        string data = "";

        while (_stream.CanRead)
        {
            if (_stream.DataAvailable)
            {
                data = _stream.Read();
                sb.AppendLine(data);

                if (data.Contains(_endOfFeedStr))
                {
                    _stream.Write(new string('\b', _endOfFeedStr.Length));
                    LastReceivedRaw = sb.ToString();
                    return;
                }

                loopCounter = 0; // reset
            }
            else
            {
                // Early exit detection
                if (loopCounter == earlyExitCheckThreshold && !string.IsNullOrEmpty(data))
                {
                    if (data.Contains("ACCESS CODE:") || data.Contains("Logged out"))
                    {
                        throw new Exception("Detected logout or prompt for re-authentication.");
                    }
                }

                Thread.Sleep(10);
                loopCounter++;

                if (loopCounter >= MaxReadLoops)
                {
                    throw new TimeoutException("Timed out waiting for RPMS output.");
                }
            }
        }

        throw new IOException("SSH stream is no longer readable.");
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

    public void GoToMainMenu(int attempts = 30)
    {
        for (int i = 0; i < attempts; i++)
        {
            var curPrompt = CurrentPrompt;

            if (curPrompt.Contains("AutoCAC App Main Menu Option:"))
                return;

            if (curPrompt.Contains("Please enter your CURRENT verify code", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Reset verify code in RPMS");

            if (curPrompt.Contains("return", StringComparison.OrdinalIgnoreCase) ||
                curPrompt.Contains("do you wish to resume", StringComparison.OrdinalIgnoreCase))
            {
                Send();
            }
            else if (curPrompt.Contains("Select DIVISION", StringComparison.OrdinalIgnoreCase))
            {
                Send(" ");
            }
            else if (curPrompt.Contains("to stop", StringComparison.OrdinalIgnoreCase) ||
                     curPrompt.Contains("halt?", StringComparison.OrdinalIgnoreCase))
            {
                Send("^");
            }
            else if (curPrompt.Contains("option", StringComparison.OrdinalIgnoreCase))
            {
                Send("^AutoCAC App Main Menu");
            }
            else
            {
                Send("^");
            }

            if (attempts - i <= 3)
            {
                Thread.Sleep(200); // let RPMS catch up
            }
        }

        throw new Exception("Could not reach main menu. Ask IRM/CAC/Informatics to assign secondary menu option: AutoCAC App Main Menu");
    }

    public void Menu(string menuName = null)
    {
        GoToMainMenu();
        if (menuName != null)
        {
            Send(menuName);
        }
    }

    public void Close()
    {
        _signedIn = false;
        _stream?.Dispose();
        _client?.Disconnect();
        _client?.Dispose();
    }

    public void Dispose()
    {
        Close();
    }
}
