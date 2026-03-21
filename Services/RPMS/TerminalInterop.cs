using Microsoft.JSInterop;
namespace AutoCAC.Services;

public class TerminalInterop
{
    private readonly RPMSService _rpms;
    private readonly UserContextService _user;
    public TerminalInterop(RPMSService rpms, UserContextService user)
    {
        _user = user;
        _rpms = rpms;
    }

    [JSInvokable]
    public async Task UserInput(string input)
    {
        if (!_user.IsAllowedRPMSInput || _rpms.BlockUserInput) return;
        if (!_rpms.CurrentMode.SignedIn)
        {
            await _rpms.Login();
            return;
        }
        try
        {
            if (_rpms.IsInMode(RPMSMode.Report, RPMSMode.ReportPrompt))
            {
                _rpms.Output.BufferFrozen = false;
                _rpms.SetMode(RPMSMode.DefaultInput);
            }

            bool finishedWriting = _rpms.CurrentMode == RPMSMode.DefaultInput && input.EndsWith("\r");

            _rpms.SendRaw(input);

            if (finishedWriting)
            {
                _rpms.SetMode(RPMSMode.DefaultReceive);
                _rpms.SendRaw(_rpms.EndOfFeedStr);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is NullReferenceException)
        {
            await _rpms.Login();
        }
    }
}
