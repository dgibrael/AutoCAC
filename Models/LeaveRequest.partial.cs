using AutoCAC.Services;
namespace AutoCAC.Models;

public partial class LeaveRequest
{
    public LeaveRequestStatusEnum StatusEnum => Enum.TryParse<LeaveRequestStatusEnum>(Status,
                true,
                out var value)
                ? value
                : LeaveRequestStatusEnum.Pending;
    public LeaveRequestTypeEnum LeaveTypeEnum => Enum.TryParse<LeaveRequestTypeEnum>(LeaveType,
                true,
                out var value)
                ? value
                : LeaveRequestTypeEnum.Annual;
}
