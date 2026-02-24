using Radzen;
namespace AutoCAC.Common;

public static class DialogDefaults
{
    public static DialogOptions Options => new DialogOptions
    {
        Width = "75%",
        CloseDialogOnOverlayClick = true,
        Resizable = true,
        Draggable = true,
        ShowClose = true,
        ShowTitle = true,
    };
}
