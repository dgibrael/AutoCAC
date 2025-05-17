using Microsoft.JSInterop;

public static class TerminalInterop
{
    public static RPMSService RPMS { get; set; }

    [JSInvokable]
    public static Task UserInput(string input)
    {
        if (RPMS.CurrentMode == RPMSMode.Disconnected)
        {
            RPMS.OpenConnection();
            return Task.CompletedTask;
        }
        else if (RPMS.CurrentMode == RPMSMode.Report)
        {
            return Task.CompletedTask;
        }
        bool recordInput = RPMS.CurrentMode == RPMSMode.DefaultInput && input.EndsWith("\r");
        try
        {
            if (recordInput)
            {
                RPMS.Stream?.Write(input);
                RPMS.CurrentMode = RPMSMode.DefaultReceive;
                RPMS.Stream?.Write(RPMS.EndOfFeedStr);
            }
            else
            {
                RPMS.Stream?.Write(input);
            }
        }
        catch (ObjectDisposedException)
        {
            RPMS.CurrentMode = RPMSMode.Disconnected;
            RPMS.OpenConnection();
        }
        return Task.CompletedTask;
    }

}
