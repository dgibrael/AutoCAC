using Microsoft.JSInterop;

public static class TerminalInterop
{
    public static RPMSService RPMS { get; set; }

    [JSInvokable]
    public static Task UserInput(string input)
    {
        if (RPMS.IsInMode(RPMSService.Modes.Disconnected))
        {
            RPMS.OpenConnection();
            return Task.CompletedTask;
        }
        else if (RPMS.IsInMode(RPMSService.Modes.Report))
        {
            return Task.CompletedTask;
        }
        bool recordInput = RPMS.IsInMode(RPMSService.Modes.DefaultInput) && input.EndsWith("\r");
        try
        {
            if (recordInput)
            {
                RPMS.SendRaw(input);
                RPMS.SetMode(RPMSService.Modes.DefaultReceive);
                RPMS.SendRaw(RPMS.EndOfFeedStr);
            }
            else
            {
                RPMS.SendRaw(input);
            }
        }
        catch (ObjectDisposedException)
        {
            RPMS.SetMode(RPMSService.Modes.Disconnected);
            RPMS.OpenConnection();
        }
        return Task.CompletedTask;
    }

}
