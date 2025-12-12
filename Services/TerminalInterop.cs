using Microsoft.JSInterop;

public class TerminalInterop
{
    private readonly RPMSService _rpms;

    public TerminalInterop(RPMSService rpms)
    {
        _rpms = rpms;
    }

    [JSInvokable]
    public Task UserInput(string input)
    {
        if (_rpms.IsInMode(RPMSService.Modes.Disconnected))
        {
            _rpms.OpenConnection();
            return Task.CompletedTask;
        }
        else if (_rpms.IsInMode(RPMSService.Modes.Report) ||
                 _rpms.IsInMode(RPMSService.Modes.ReportPrompt))
        {
            _rpms.Output.BufferFrozen = false;
            _rpms.SetMode(RPMSService.Modes.DefaultInput);
        }

        bool finishedWriting = _rpms.IsInMode(RPMSService.Modes.DefaultInput) && input.EndsWith("\r");

        try
        {
            if (finishedWriting)
            {
                _rpms.SendRaw(input);
                _rpms.SetMode(RPMSService.Modes.DefaultReceive);
                _rpms.SendRaw(_rpms.EndOfFeedStr);
            }
            else
            {
                _rpms.SendRaw(input);
            }
        }
        catch (ObjectDisposedException)
        {
            _rpms.SetMode(RPMSService.Modes.Disconnected);
            _rpms.OpenConnection();
        }

        return Task.CompletedTask;
    }
}
