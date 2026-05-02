using AutoCAC.Extensions;
using AutoCAC.Services;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;

namespace AutoCAC.Models;

public enum DrugReqStatusEnum
{
    UnSubmitted,
    Submitted,
    Complete
}

public partial class DrugRequest
{
    public static async Task<DrugRequest> Create(
        IDbContextFactory<MainContext> dbFactory,
        string ndc,
        int userId
        )
    {
        var now = DateTime.Now;
        var status = DrugReqStatusEnum.UnSubmitted.ToString();
        var drugRequest = new DrugRequest()
        {
            CreatedAt = now,
            CurrentStatus = status,
            Ndc = ndc
        };
        await dbFactory.AddItemAsync(drugRequest);
        await drugRequest.AddActivity(dbFactory, userId, status, currentDateTime: now);
        return drugRequest;
    }
    public async Task AddActivity(
        IDbContextFactory<MainContext> dbFactory,
        int userId,
        string newStatus,
        string prevStatus = null,
        DateTime? currentDateTime = null,
        EmailService emailService = null,
        HashSet<string> emails = null
        )
    {
        var now = currentDateTime ?? DateTime.Now;
        var activity = new DrugRequestActivity()
        {
            DrugRequestId = Id,
            AuthUserId = userId,
            ActivityAt = now,
            NewStatus = newStatus,
            PreviousStatus = prevStatus
        };
        await dbFactory.AddItemAsync(activity);
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.AuthUsers.FirstOrDefaultAsync(x => x.Id == userId);
        if (emailService != null && emails != null)
        {
            string subject = "";
            string msg = $"Ndc: {Ndc};";
            msg += "<br>";

            if (DrugId == null)
            {
                var ndf = await db.Ndfs.OrderByDescending(x => x.PrintName).FirstOrDefaultAsync(x => x.Ndc == Ndc);
                if (ndf == null)
                {
                    subject = "Drug Request See Comments in full request";
                    msg += "Could not match NDC to National Drug File";
                }
                else
                {
                    subject = $"Drug request for {ndf?.PrintName}";
                    msg += $"NDF Print name: {ndf?.PrintName}, NDF Generic: {ndf?.Generic}";
                }
            }
            else
            {
                subject = $"Drug update request for {Drug?.Name}";
                msg += $"Update Drug: {Drug?.Name} (ien: {DrugId})";
            }
            subject += $" Marked as {newStatus} by {user?.DisplayName}";
            msg += $"<br><a href=\"https://navchc-rx.d1.na.ihs.gov:8443/drugrequest/{Id}\">Click here to view full request</a>";
            await emailService.SendEmailAsync(subject, msg, emails.ToArray());
        }
    }
}