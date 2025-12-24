using AutoCAC.Models;
using AutoCAC.Utilities;
using Dapper;
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
using System.Reflection;
using System.Threading.Tasks;
namespace AutoCAC.Extensions
{
    public static class DbFactoryExtensions
    {
        public static async Task<T> GetFirstValueAsync<T>(
            this IDbContextFactory<mainContext> factory,
            FormattableString sql)
        {
            await using var context = factory.CreateDbContext();
            var connection = context.Database.GetDbConnection();
            var (qry, parameters) = sql.ToSqlAndParams();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync(); 
            return await connection.ExecuteScalarAsync<T>(qry, parameters);
        }
        public static async Task<string> GetFirstValueAsync(
            this IDbContextFactory<mainContext> factory,
            FormattableString sql)
        {
            return await factory.GetFirstValueAsync<string>(sql);
        }

        public static async Task<List<T>> ReadSqlAsync<T>(
            this IDbContextFactory<mainContext> factory,
            FormattableString sql) where T : class
        {
            await using var context = factory.CreateDbContext();
            return await context.Database.SqlQuery<T>(sql).ToListAsync();
        }

        public static async Task<int> ExecuteSqlAsync(
            this IDbContextFactory<mainContext> factory,
            FormattableString sql)
        {
            await using var context = factory.CreateDbContext();
            return await context.Database.ExecuteSqlInterpolatedAsync(sql);
        }

        public static async Task<int> ExecuteSqlTransactionsAsync(
            this IDbContextFactory<mainContext> factory,
            IEnumerable<FormattableString> commands)
        {
            await using var context = factory.CreateDbContext();
            var db = context.Database;
            var connection = db.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();
            db.UseTransaction(transaction); // Attach transaction to EF context

            var totalAffectedRows = 0;

            try
            {
                foreach (var sql in commands)
                {
                    var affected = await db.ExecuteSqlInterpolatedAsync(sql);
                    totalAffectedRows += affected;
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return totalAffectedRows;
        }
        public static async Task DeleteItemAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            TEntity item,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            db.Attach(item);
            db.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }

        public static async Task DeleteRangeAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            IEnumerable<TEntity> items,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            db.AttachRange(items);
            db.RemoveRange(items);
            await db.SaveChangesAsync(cancellationToken);
        }
        /// <summary>
        /// Adds a single entity and saves. Returns the tracked entity with generated keys populated.
        /// </summary>
        public static async Task<TEntity> AddItemAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            TEntity item,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await db.Set<TEntity>().AddAsync(item, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return item;
        }

        /// <summary>
        /// Adds a range of entities and saves. Returns the input sequence to allow further use with populated keys.
        /// </summary>
        public static async Task<IEnumerable<TEntity>> AddRangeAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            IEnumerable<TEntity> items,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await db.Set<TEntity>().AddRangeAsync(items, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return items;
        }

        /// <summary>
        /// Updates an entity (all properties marked modified) and saves.
        /// Use when you have a detached instance coming from the UI.
        /// </summary>
        public static async Task UpdateItemAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            TEntity item,
            CancellationToken cancellationToken = default)
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

            // 2. Normal EF attach/update
            await using var db = await factory.CreateDbContextAsync(cancellationToken);

            db.Attach(item);
            var entry = db.Entry(item);

            // 3. Mark only non-key scalars (including FKs) as modified
            foreach (var propMeta in entry.Metadata.GetProperties())
            {
                if (!propMeta.IsPrimaryKey())
                    entry.Property(propMeta.Name).IsModified = true;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        public static async Task<bool> UpdateItemWithConcurrencyAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            TEntity item,
            DialogService dialogs,
            CancellationToken ct = default)
            where TEntity : class
        {
            // 1) keep your nav-nulling, but don't null concurrency tokens
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

            // 2) attach and mark modified (same as yours), but skip concurrency tokens
            await using var db = await factory.CreateDbContextAsync(ct);
            db.Attach(item);
            var entry = db.Entry(item);

            var concurrencyProps = entry.Metadata.GetProperties()
                .Where(p => p.IsConcurrencyToken)
                .Select(p => p.Name)
                .ToHashSet();

            foreach (var propMeta in entry.Metadata.GetProperties())
            {
                if (propMeta.IsPrimaryKey()) continue;

                var name = propMeta.Name;
                var p = entry.Property(name);

                if (concurrencyProps.Contains(name))
                {
                    p.IsModified = false;
                    p.OriginalValue = type.GetProperty(name)?.GetValue(item);
                }
                else
                {
                    p.IsModified = true;
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return true; // saved normally
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // record changed or deleted since the form loaded
                var conflicted = ex.Entries.Single(); // single entity in your helper

                // If deleted
                var databaseValues = await conflicted.GetDatabaseValuesAsync(ct);
                if (databaseValues == null)
                {
                    await dialogs.Alert("The record was deleted by another operation.", "Concurrency conflict");
                    conflicted.State = EntityState.Detached;
                    return false;
                }

                // Ask user
                var overwrite = await dialogs.Confirm(
                    "This record was changed by another operation. Overwrite the current database values with your changes?",
                    "Concurrency conflict",
                    new ConfirmOptions { OkButtonText = "Overwrite", CancelButtonText = "Cancel" });

                if (overwrite == true)
                {
                    // Client wins: use latest token, keep current values
                    conflicted.OriginalValues.SetValues(databaseValues); // refresh original RowVersion
                                                                         // keep your IsModified flags as set above
                    await db.SaveChangesAsync(ct);
                    return true;
                }
                else
                {
                    // Store wins: refresh item from DB and exit
                    conflicted.CurrentValues.SetValues(databaseValues);
                    conflicted.State = EntityState.Unchanged;
                    return false;
                }
            }
        }

        public static async Task<bool> UpdateItemForceConcurrencyAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            TEntity item,
            CancellationToken ct = default)
            where TEntity : class
        {
            // 1) Null navs, but keep rowversion/timestamp
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

            // 2) Attach and mark modified, skip concurrency tokens
            await using var db = await factory.CreateDbContextAsync(ct);
            db.Attach(item);
            var entry = db.Entry(item);

            var concurrencyProps = entry.Metadata.GetProperties()
                .Where(p => p.IsConcurrencyToken)
                .Select(p => p.Name)
                .ToHashSet();

            foreach (var propMeta in entry.Metadata.GetProperties())
            {
                if (propMeta.IsPrimaryKey()) continue;

                var name = propMeta.Name;
                var p = entry.Property(name);

                if (concurrencyProps.Contains(name))
                {
                    p.IsModified = false; // do not try to write the token
                                          // no need to set OriginalValue yet
                }
                else
                {
                    p.IsModified = true;
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return true; // no conflict
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var conflicted = ex.Entries.Single(); // expect single entity here
                var databaseValues = await conflicted.GetDatabaseValuesAsync(ct);

                if (databaseValues == null)
                {
                    return false;
                }

                // Force overwrite: keep current values, refresh token to latest
                conflicted.OriginalValues.SetValues(databaseValues);
                // token now matches DB; write current values
                await db.SaveChangesAsync(ct);
                return true;
            }
        }

        public static async Task<TEntity> GetByPkAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            CancellationToken cancellationToken = default,
            params object[] keyValues)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            return await db.Set<TEntity>().FindAsync(keyValues, cancellationToken);
        }

        public static async Task<TEntity> GetByExpressionAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            Expression<Func<TEntity, bool>> predicate,
            bool readOnly = false,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var query = db.Set<TEntity>().AsQueryable();
            if (readOnly) query = query.AsNoTracking();
            return await query.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public static async Task<TEntity> GetByPkAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            object id,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            return await factory.GetByPkAsync<TEntity>(cancellationToken, id);
        }

        public static async Task<bool> DeleteByPkAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            CancellationToken cancellationToken = default,
            params object[] keyValues)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var entity = await db.Set<TEntity>().FindAsync(keyValues, cancellationToken);
            if (entity == null) return false;
            db.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public static Task<bool> DeleteByPkAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            object id,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            // forward to the params overload
            return factory.DeleteByPkAsync<TEntity>(cancellationToken, id);
        }

        public static async Task<bool> ExistsAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
            where TEntity : class
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            return await db.Set<TEntity>()
                .AsNoTracking()
                .AnyAsync(predicate, cancellationToken);
        }

        public static object GetPrimaryKeyValue<TEntity>(
            this IDbContextFactory<mainContext> factory,
            TEntity entity)
            where TEntity : class
        {
            if (entity is null) return null;

            using var db = factory.CreateDbContext(); // cheap factory context
            var et = db.Model.FindEntityType(typeof(TEntity))
                     ?? throw new InvalidOperationException($"No entityType for {typeof(TEntity).Name}");
            var pk = et.FindPrimaryKey()
                     ?? throw new InvalidOperationException($"No PK for {typeof(TEntity).Name}");
            if (pk.Properties.Count != 1)
                throw new NotSupportedException("Composite keys not supported.");

            var propInfo = pk.Properties[0].PropertyInfo
                           ?? throw new InvalidOperationException("PK property has no PropertyInfo.");
            return propInfo.GetValue(entity);
        }

        public static async Task NavigateToEdit<TEntity>(
            this IDbContextFactory<mainContext> factory,
            NavigationManager nav,
            TEntity entity)
            where TEntity : class
        {
            var pk = factory.GetPrimaryKeyValue(entity);
            if (pk is null) return;
            nav.NavigateTo(nav.GetPath(pk.ToString()));
        }

        public static async Task SaveNavigationItemsAsync<TEntity>(
            this IDbContextFactory<mainContext> factory,
            TEntity item,
            params Expression<Func<TEntity, object>>[] navigationProperties)
            where TEntity : class, new()
        {
            await using var db = await factory.CreateDbContextAsync();

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

            await db.SaveChangesAsync();
        }

        // Helper to extract PropertyInfo from expression
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

    }
}

