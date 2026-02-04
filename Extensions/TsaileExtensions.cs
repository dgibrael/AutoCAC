using AutoCAC.Models;
using AutoCAC.Utilities;
using Dapper;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
namespace AutoCAC.Extensions.Tsaile
{
    public static class TsaileExtensions
    {
        public static async Task<TsaileBetterq> UpdateStatusAsync(
            this IDbContextFactory<mainContext> dbFactory,
            long ticketId,
            string newStatus,
            int userId,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var ticket = await db.TsaileBetterqs.FirstOrDefaultAsync(x => x.Id == ticketId);
            return await db.UpdateStatusAsync(ticket, newStatus, userId, ct);
        }

        public static async Task<TsaileBetterq> UpdateStatusAsync(
            this mainContext db,
            TsaileBetterq ticket,
            string newStatus,
            int userId,
            CancellationToken ct = default)
        {
            if (ticket.Status == newStatus) return ticket;
            var oldStatus = ticket.Status;
            ticket.Status = newStatus;
            var curDateTime = DateTime.Now;
            if (ticket.StatusEnum is (TsaileTicketStatus.Complete or TsaileTicketStatus.Deleted)) ticket.CompletedDateTime = curDateTime;
            else ticket.CompletedDateTime = null;
            ticket.LastModifiedDateTime = curDateTime;
            var a = new TsaileActivitylog
            {
                TsaileBetterqId = ticket.Id,
                AuthUserId = userId,
                ChangedAt = curDateTime,
                ChangedFrom = oldStatus,
                ChangedTo = ticket.Status
            };
            await db.UpdateItemAsync(ticket);
            await db.AddItemAsync(a, ct);
            await db.SaveChangesAsync(ct);
            return ticket;
        }

        public static async Task<TsaileBetterq> AdvanceStatusAndSaveAsync(
            this IDbContextFactory<mainContext> dbFactory,
            long ticketId,
            int userId,
            IReadOnlyList<string> skipVerify,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var ticket = await db.TsaileBetterqs.FirstOrDefaultAsync(x => x.Id == ticketId);
            var nextStatus = ticket.NextStatusEnum;
            if (ticket.StatusEnum == TsaileTicketStatus.Verifying)
            {
                var skipStep = skipVerify.FirstOrDefault(x => ticket.BatchTo == x);
                if (skipStep != null)
                {
                    nextStatus = nextStatus.NextStatus();
                }
            }
            return await db.UpdateStatusAsync(ticket, nextStatus.ToString(), userId, ct);
        }

        public static async Task<TsaileBetterq> ReverseStatusAndSaveAsync(
            this IDbContextFactory<mainContext> dbFactory,
            long ticketId,
            int userId,
            IReadOnlyList<string> skipVerify,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var ticket = await db.TsaileBetterqs.FirstOrDefaultAsync(x => x.Id == ticketId);
            var prevStatus = ticket.PreviousStatusEnum;
            if (ticket.StatusEnum == TsaileTicketStatus.Complete)
            {
                var skipStep = skipVerify.FirstOrDefault(x => ticket.BatchTo == x);
                if (skipStep != null)
                {
                    prevStatus = prevStatus.PreviousStatus();
                }
            }
            return await db.UpdateStatusAsync(ticket, prevStatus.ToString(), userId, ct);        
        }

        public static async Task<TsaileBetterq> AddCommentAsync(
            this IDbContextFactory<mainContext> dbFactory,
            long ticketId,
            string comment,
            int userId,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var ticket = await db.TsaileBetterqs.FirstOrDefaultAsync(x => x.Id == ticketId);
            var curDateTime = DateTime.Now;
            var a = new TsaileComment
            {
                TsaileBetterqId = ticket.Id,
                AuthUserId = userId,
                CreatedAt = curDateTime,
                Remarks = comment
            };
            await db.AddItemAsync(a);
            await db.SaveChangesAsync(ct);
            return ticket;
        }

        public static async Task<TsaileBetterq> CreateAsync(
            this IDbContextFactory<mainContext> dbFactory,
            long ticketId,
            int userId,
            CancellationToken ct = default)
        {

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var ticket = await db.TsaileBetterqs.FirstOrDefaultAsync(x => x.Id == ticketId);
            var curDateTime = DateTime.Now;
            ticket.LastModifiedDateTime = curDateTime;
            ticket.CreatedDateTime = curDateTime;
            await db.AddItemAsync(ticket, ct);
            var a = new TsaileActivitylog
            {
                TsaileBetterqId = ticketId,
                AuthUserId = userId,
                ChangedAt = curDateTime,
                ChangedTo = ticket.Status
            };
            await db.AddItemAsync(a, ct);
            await db.SaveChangesAsync(ct);
            return ticket;
        }

    }
}

