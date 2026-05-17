using AutoCAC.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AutoCAC.Components.Bases;

public abstract class ChildTabComponent : ComponentBase
{
    [Inject]
    protected IJSRuntime JS { get; set; }

    [SupplyParameterFromQuery]
    public string Channel { get; set; } = "";

    protected async Task CloseTabAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            return;
        }

        await JS.PostBroadcastMessageAsync(
            Channel,
            message,
            closeAfterPost: true);
    }
    public bool HasChannel => !string.IsNullOrWhiteSpace(Channel);
}