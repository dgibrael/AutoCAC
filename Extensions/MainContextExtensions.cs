using AutoCAC.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Radzen;
using System;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
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
            string jsonSettings,
            bool isShared = false,
            CancellationToken ct = default)
        {
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
                    DataGridSettings = jsonSettings
                };

                db.DataGridTemplates.Add(entity);
            }
            else
            {
                entity.IsShared = isShared;
                entity.DataGridSettings = jsonSettings;
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

        public static async Task UpdateItemAsync<TEntity>(
            this mainContext db,
            TEntity item)
            where TEntity : class
        {
            // 1. Remove navigation objects before attaching
            var type = typeof(TEntity);
            foreach (var prop in type.GetProperties())
            {
                bool isCollection = typeof(IEnumerable).IsAssignableFrom(prop.PropertyType)
                                    && prop.PropertyType != typeof(string)
                                    && prop.PropertyType != typeof(byte[]);

                bool isTimestamp = prop.GetCustomAttributes(typeof(TimestampAttribute), true).Any();

                // Null only EF navigation properties — not scalars, byte[], strings, or [Timestamp]
                if (!prop.PropertyType.IsValueType &&
                    prop.PropertyType != typeof(string) &&
                    prop.PropertyType != typeof(byte[]) &&
                    !isCollection &&
                    !isTimestamp)
                {
                    prop.SetValue(item, null);
                }
            }

            db.Attach(item);
            var entry = db.Entry(item);

            // 3. Mark only non-key scalars (including FKs) as modified
            foreach (var propMeta in entry.Metadata.GetProperties())
            {
                if (!propMeta.IsPrimaryKey())
                    entry.Property(propMeta.Name).IsModified = true;
            }
        }

        public static async Task SaveNavigationItemsAsync<TEntity>(
            this mainContext db,
            TEntity item,
            params Expression<Func<TEntity, object>>[] navigationProperties)
            where TEntity : class, new()
        {
            var entityType = db.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException($"Entity {typeof(TEntity).Name} not found.");

            // Build query with typed includes
            IQueryable<TEntity> query = db.Set<TEntity>();
            foreach (var nav in navigationProperties)
            {
                query = query.Include(nav);
            }

            // Load key
            var keyProp = entityType.FindPrimaryKey().Properties.Single();
            var keyClrProp = typeof(TEntity).GetProperty(keyProp.Name)
                ?? throw new InvalidOperationException($"Key property {keyProp.Name} not found.");

            var keyValue = keyClrProp.GetValue(item)
                ?? throw new InvalidOperationException("Primary key value is null.");

            // Load existing entity with navigations
            var original = await query.FirstOrDefaultAsync(
                e => EF.Property<object>(e, keyProp.Name).Equals(keyValue));

            if (original == null)
                throw new InvalidOperationException("Entity no longer exists.");

            // Update scalar properties
            db.Entry(original).CurrentValues.SetValues(item);

            // Replace navigation collections
            foreach (var navExp in navigationProperties)
            {
                var prop = GetPropertyInfo(navExp);

                var newCollection = prop.GetValue(item) as IEnumerable;
                var originalCollection = prop.GetValue(original) as IList;

                if (originalCollection == null)
                    throw new InvalidOperationException(
                        $"Navigation property '{prop.Name}' must be assignable to IList.");

                originalCollection.Clear();

                foreach (var element in newCollection)
                    originalCollection.Add(element);
            }
        }

        private static PropertyInfo GetPropertyInfo<TEntity>(
            Expression<Func<TEntity, object>> expression)
        {
            MemberExpression memberExp = expression.Body switch
            {
                MemberExpression m => m,
                UnaryExpression u when u.Operand is MemberExpression m => m,
                _ => throw new InvalidOperationException("Invalid navigation expression.")
            };

            return (PropertyInfo)memberExp.Member;
        }

        public static async Task AddItemAsync<TEntity>(
            this mainContext db,
            TEntity item,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            // 1. Remove navigation objects before Add
            var type = typeof(TEntity);
            foreach (var prop in type.GetProperties())
            {
                bool isCollection =
                    typeof(IEnumerable).IsAssignableFrom(prop.PropertyType)
                    && prop.PropertyType != typeof(string)
                    && prop.PropertyType != typeof(byte[]);

                bool isTimestamp =
                    prop.GetCustomAttributes(typeof(TimestampAttribute), true).Any();

                // Null only EF navigation properties
                if (!prop.PropertyType.IsValueType &&
                    prop.PropertyType != typeof(string) &&
                    prop.PropertyType != typeof(byte[]) &&
                    !isCollection &&
                    !isTimestamp)
                {
                    prop.SetValue(item, null);
                }
            }
            await db.Set<TEntity>().AddAsync(item, cancellationToken);
        }

        public static async Task DeleteItemAsync<TEntity>(
            this mainContext db,
            TEntity item)
            where TEntity : class
        {
            db.Attach(item);
            db.Remove(item);
        }

    }
}

