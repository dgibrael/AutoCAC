#nullable enable

using System.ComponentModel.DataAnnotations.Schema;

namespace AutoCAC.Models;

public partial class BlisterPackPatientActivity : IActivityLog<int>
{
    [NotMapped] public string? ChangedField { get; set; } = null;
}