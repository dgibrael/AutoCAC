using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
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
    }
}

