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
        try
        {
            RPMS.Stream?.Write(input);
        }
        catch (ObjectDisposedException)
        {
            RPMS.CurrentMode = RPMSMode.Disconnected;
            RPMS.OpenConnection();
        }
        return Task.CompletedTask;
    }

}
