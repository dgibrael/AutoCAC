using AutoCAC.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoCAC.Extensions
{
    public static class MainContextExtensions
    {
        public static async Task SetMenuBuildMetaStatusAsync(this mainContext db, int id, string newStatus = "CHANGES REQUESTED")
        {
            var menu = await db.MenuBuildMeta.FirstOrDefaultAsync(m => m.Id == id);

            if (menu is null)
                throw new InvalidOperationException($"MenuBuildMeta with ID {id} was not found.");

            if (menu.RequestStatus == newStatus)
                return; // No update needed

            menu.RequestStatus = newStatus;
            await db.SaveChangesAsync();
        }

        public static Task<List<DataGridTemplate>> GetDataGridTemplatesAsync(
            this mainContext db,
            string dataGridName,
            string username,
            bool includeShared = true,
            CancellationToken ct = default) =>
            db.DataGridTemplates
              .AsNoTracking()
              .Where(t => t.DataGridName == dataGridName &&
                         (t.CreatedBy == username || (includeShared && t.IsShared)))
              .OrderBy(t => t.TemplateName)
              .ToListAsync(ct);

        public static async Task<DataGridTemplate> UpsertDataGridTemplate(
            this mainContext db,
            string templateName,
            string dataGridName,
            string createdBy,
            DataGridSettings dataGridSettings,
            bool isShared = false,
            CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(dataGridSettings);

            var entity = await db.DataGridTemplates
                .FirstOrDefaultAsync(t =>
                    t.TemplateName == templateName &&
                    t.CreatedBy == createdBy &&
                    t.DataGridName == dataGridName, ct);

            if (entity is null)
            {
                entity = new DataGridTemplate
                {
                    TemplateName = templateName,
                    CreatedBy = createdBy,
                    DataGridName = dataGridName,
                    IsShared = isShared,
                    DataGridSettings = json
                };

                db.DataGridTemplates.Add(entity);
            }
            else
            {
                entity.IsShared = isShared;
                entity.DataGridSettings = json;
                // no need to call Update; tracked entity will be saved
            }

            await db.SaveChangesAsync(ct);
            return entity; // includes generated Id after SaveChanges
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

