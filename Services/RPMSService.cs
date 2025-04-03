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
    private string _lastSent;
    private string _lastReceivedRaw;
    public string LastReceivedRaw 
    { 
        get => _lastReceivedRaw; 
        private set => _lastReceivedRaw = value?.Trim().TrimEnd(_endOfFeedStr.ToCharArray()).Trim();
    }
    private readonly string _endOfFeedStr = ((char)255).ToString();
    public event Action OnConnected;
    private bool _signedIn;
    public bool IsConnected
    {
        get
        {
            if (_client?.IsConnected != true)
            {
                _signedIn = false;
                return false;
            }
            return _signedIn;
        }
    }
    public RPMSService()
    {
        _signedIn = false;
    }

    public void OpenConnection()
    {
        _client = new SshClient("CHC-RPMS", "m", "");
        _client.Connect();

        var terminalModes = new Dictionary<TerminalModes, uint>();
        _stream = _client.CreateShellStream("dumb", 80, 24, 800, 600, 1024, terminalModes);

        WaitFor("ACCESS CODE");
    }

    public void Login(Span<char> accessCode, Span<char> verifyCode)
    {
        if (!IsConnected)
        {
            OpenConnection();
        }
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
                    GoToMainMenu();
                    _signedIn = true;
                    OnConnected?.Invoke();
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
            _lastSent = "*hidden*";
            input.Clear();
        }
    }

    public string Menu(string menuName, string? choice = null)
    {
        GoToMainMenu();
        Send("^" + menuName);
        if (!string.IsNullOrWhiteSpace(choice) && CurrentPrompt.ToLower().Contains("choose"))
        {
            Send(choice);
        }

        return LastReceivedRaw;
    }

    public void Send(string command = "")
    {
        if (_client?.IsConnected != true)
        {
            throw new Exception("Disconnected");
        }

        SendRaw(command);
        Read();
    }

    private void SendRaw(string command)
    {
        _lastSent = command;
        _stream.Write(command+"\r");
    }

    private void Read()
    {
        _stream.Write(_endOfFeedStr);
        var sb = new StringBuilder();
        while (true)
        {
            if (_stream.DataAvailable)
            {
                var data = _stream.Read();
                sb.AppendLine(data);
                if (data.Contains(_endOfFeedStr))
                {
                    _stream.Write(new string('\b', _endOfFeedStr.Length));
                    LastReceivedRaw = sb.ToString();
                    break;
                }
            }
        }
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

    public string GoToMainMenu(int attempts = 30)
    {
        var curPrompt = CurrentPrompt;
        if (curPrompt.Contains("AutoCAC App Main Menu Option:"))
        {
            return curPrompt;
        }
        else if (curPrompt.Contains("Please enter your CURRENT verify code", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Reset verify code in RPMS");
        }
        else if (curPrompt.ToLower().Contains("return") || curPrompt.ToLower().Contains("do you wish to resume"))
        {
            Send();
        }
        else if (curPrompt.Contains("Select DIVISION", StringComparison.OrdinalIgnoreCase))
        {
            Send(" ");
        }
        else if (curPrompt.ToLower().Contains("to stop") || curPrompt.ToLower().Contains("halt?"))
        {
            Send("^");
        }
        else if (curPrompt.ToLower().Contains("option"))
        {
            Send("^AutoCAC App Main Menu");
        }
        else
        {
            Send("^");
        }

        if (attempts > 0)
        {
            return GoToMainMenu(attempts - 1);
        }
        else
        {
            throw new Exception("Could not reach main menu. Ask IRM/CAC/Informatics to assign secondary menu option: AutoCAC App Main Menu");
        }
    }

    public void Close()
    {
        _stream?.Dispose();
        _client?.Disconnect();
        _client?.Dispose();
    }

    public void Dispose()
    {
        Close();
    }
}
