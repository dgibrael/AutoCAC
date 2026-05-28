#nullable enable

using System.ComponentModel.DataAnnotations.Schema;

namespace AutoCAC.Models;

public partial class BlisterPackFillActivity : IActivityLog
{
    [NotMapped] public string? ChangedField { get; set; } = "";
}
