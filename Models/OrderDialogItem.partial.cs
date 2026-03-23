using AutoCAC.Extensions;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace AutoCAC.Models;

public partial class OrderDialogItem
{
    public string ActualDisplayedText => string.IsNullOrWhiteSpace(DisplayText) ? ChildOrderDialog?.DisplayText : DisplayText;
}
