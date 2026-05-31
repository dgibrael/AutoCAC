using System.ComponentModel.DataAnnotations.Schema;

namespace AutoCAC.Models;


public enum BlisterPackFillStatus
{
    Scheduled,
    InProcess,
    Exception,
    Completed
}

public partial class BlisterPackFill
{
    [NotMapped] public BlisterPackFillStatus StatusEnum
    {
        get => Enum.TryParse<BlisterPackFillStatus>(Status, out var result) ? result : BlisterPackFillStatus.Scheduled;
        set => Status = value.ToString();
    }
}
