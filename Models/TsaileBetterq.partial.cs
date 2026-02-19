using AutoCAC.Extensions;
using Microsoft.EntityFrameworkCore;
namespace AutoCAC.Models;

public enum TsaileTicketStatus
{
    Screening,
    Filling,
    Verifying,
    ReadyForPickup,
    Complete,
    Exception,
    PA,
    SDR,
    Deleted
}

public static class TsaileTicketStatusExtensions
{
    public static TsaileTicketStatus NextStatus(this TsaileTicketStatus status)
    {
        return status switch
        {
            TsaileTicketStatus.Complete => status,
            TsaileTicketStatus.SDR => TsaileTicketStatus.Filling,
            TsaileTicketStatus.PA => TsaileTicketStatus.Filling,
            TsaileTicketStatus.Deleted => TsaileTicketStatus.Screening,
            TsaileTicketStatus.Exception => TsaileTicketStatus.Screening,

            _ => status + 1
        };
    }
    public static TsaileTicketStatus PreviousStatus(this TsaileTicketStatus status)
    {
        return status switch
        {
            TsaileTicketStatus.Screening => status,
            TsaileTicketStatus.SDR => TsaileTicketStatus.Filling,
            TsaileTicketStatus.PA => TsaileTicketStatus.Filling,
            TsaileTicketStatus.Deleted => TsaileTicketStatus.Screening,
            TsaileTicketStatus.Exception => TsaileTicketStatus.Screening,
            _ => status - 1
        };
    }
}


public partial class TsaileBetterq
{
    public TsaileTicketStatus StatusEnum =>
        Enum.Parse<TsaileTicketStatus>(Status, ignoreCase: true);
    public TsaileTicketStatus NextStatusEnum
    {
        get
        {
            if (StatusEnum == TsaileTicketStatus.Verifying && !Waiting) return TsaileTicketStatus.Complete;
            return StatusEnum.NextStatus();
        }
    }

    public TsaileTicketStatus PreviousStatusEnum
    {
        get
        {
            if (StatusEnum == TsaileTicketStatus.Complete && !Waiting) return TsaileTicketStatus.Verifying;
            return StatusEnum.PreviousStatus();
        }
    }
    public bool IsLocked => LockedDateTime > DateTime.Now.AddMinutes(-10);
    public async Task UpdateStatusAsync(
        IDbContextFactory<mainContext> dbFactory,
        string newStatus,
        int userId,
        CancellationToken ct = default)
    {
        if (Status == newStatus) return;
        var oldStatus = Status;
        Status = newStatus;
        var curDateTime = DateTime.Now;
        if (StatusEnum is (TsaileTicketStatus.Complete or TsaileTicketStatus.Deleted)) CompletedDateTime = curDateTime;
        else CompletedDateTime = null;
        LastModifiedDateTime = curDateTime;
        var a = new TsaileActivitylog
        {
            TsaileBetterqId = Id,
            AuthUserId = userId,
            ChangedAt = curDateTime,
            ChangedFrom = oldStatus,
            ChangedTo = Status
        };
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken: ct);
        await db.UpdateItemAsync(this);
        await db.AddItemAsync(a, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddCommentAsync(
        IDbContextFactory<mainContext> dbFactory,
        string comment,
        int userId,
        CancellationToken ct = default)
    {
        var curDateTime = DateTime.Now;
        var a = new TsaileComment
        {
            TsaileBetterqId = Id,
            AuthUserId = userId,
            CreatedAt = curDateTime,
            Remarks = comment
        };
        await dbFactory.AddItemAsync(a);
    }

    public async Task CreateAsync(
        IDbContextFactory<mainContext> dbFactory,
        int userId,
        CancellationToken ct = default)
    {
        var curDateTime = DateTime.Now;
        LastModifiedDateTime = curDateTime;
        CreatedDateTime = curDateTime;
        await dbFactory.AddItemAsync(this, ct);
        var a = new TsaileActivitylog
        {
            TsaileBetterqId = Id,
            AuthUserId = userId,
            ChangedAt = curDateTime,
            ChangedTo = Status
        };
        await dbFactory.AddItemAsync(a, ct);
    }
    public async Task<DateTime> LockAsync(
        IDbContextFactory<mainContext> dbFactory,
        int userId,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var now = DateTime.Now;

        await db.Set<TsaileBetterq>()
            .Where(e => e.Id == Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.LockedDateTime, now)
                .SetProperty(e => e.LockedById, userId),
                ct);
        return now;
    }

}
