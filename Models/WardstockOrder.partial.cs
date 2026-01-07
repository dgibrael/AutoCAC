using System.ComponentModel.DataAnnotations.Schema;
using AutoCAC.Extensions;
namespace AutoCAC.Models;

public partial class WardstockOrder
{
    [NotMapped]
    public WardstockOrderStatus StatusEnum
    {
        get => Enum.Parse<WardstockOrderStatus>(Status, ignoreCase: true);
        set => Status = value.ToString();
    }
    public bool IsLastStatus => StatusEnum.IsLast;
    public bool IsFirstStatus => StatusEnum.IsFirst;
    public string SendForward()
    {
        StatusEnum = StatusEnum.Next();
        return Status;
    }

    public string SendBack()
    {
        StatusEnum = StatusEnum.Previous();
        return Status;
    }
}

public enum WardstockOrderStatus
{
    New,
    Submitted,
    Filled,
    Checked,
    ReadyForPickup,
    Completed
}

