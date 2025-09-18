using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System;
using System.Threading.Tasks;

namespace AutoCAC.Extensions
{
    public static class MainContextExtensions
    {
        public static async Task MenuBuildMetaSetStatusAsync(this mainContext db, int id, string newStatus = "CHANGES REQUESTED")
        {
            var menu = await db.MenuBuildMeta.FirstOrDefaultAsync(m => m.Id == id);

            if (menu is null)
                throw new InvalidOperationException($"MenuBuildMeta with ID {id} was not found.");

            if (menu.RequestStatus == newStatus)
                return; // No update needed

            menu.RequestStatus = newStatus;
            await db.SaveChangesAsync();
        }
        public static async Task<bool> SaveChangesHandleConcurrencyAsync(
            this mainContext db,
            NotificationService notifications,
            string notificationHeader = "Concurrency Error",
            string notificationMsg = "This record has been edited since it was last loaded")
        {
            try
            {
                await db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                notifications.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = notificationHeader,
                    Detail = notificationMsg,
                    Duration = 4000
                });

                return false;
            }
        }
    }
}

