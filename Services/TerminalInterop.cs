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
        else if (RPMS.IsInMode(RPMSService.Modes.Report) || RPMS.IsInMode(RPMSService.Modes.ReportPrompt))
        {
            RPMS.Output.BufferFrozen = false;
            RPMS.SetMode(RPMSService.Modes.DefaultInput);
        }
        bool finishedWriting = RPMS.IsInMode(RPMSService.Modes.DefaultInput) && input.EndsWith("\r");
        try
        {
            if (finishedWriting)
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
