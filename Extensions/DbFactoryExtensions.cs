using AutoCAC.Models;
using AutoCAC.Utilities;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;
namespace AutoCAC.Extensions
{
    public static class DbFactoryExtensions
    {
        public static async Task<IEnumerable<T>> ReadSqlAsync<T>(
            this IDbContextFactory<mainContext> factory,
            string sql,
            object parameters = null)
        {
            await using var context = factory.CreateDbContext();
            var connection = context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();
            return await connection.QueryAsync<T>(sql, parameters);
        }

        public static async Task<T> GetFirstValueAsync<T>(
            this IDbContextFactory<mainContext> factory,
            string sql,
            object parameters = null)
        {
            await using var context = factory.CreateDbContext();
            var connection = context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();
            return await connection.ExecuteScalarAsync<T>(sql, parameters);
        }

        public static Task<string> GetFirstValueAsync(
            this IDbContextFactory<mainContext> factory,
            string sql,
            object parameters = null) =>
            factory.GetFirstValueAsync<string>(sql, parameters);

        public static IQueryable<T> QueryFromSql<T>(
            this IDbContextFactory<mainContext> factory,
            FormattableString sql) where T : class
        {
            var context = factory.CreateDbContext();
            return context.Set<T>().FromSqlInterpolated(sql);
        }

        public static IQueryable<T> QueryFromObj<T>(
            this IDbContextFactory<mainContext> factory) where T : class
        {
            var context = factory.CreateDbContext();
            return context.Set<T>();
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
                // skip collections, keep only reference navigations
                if (!prop.PropertyType.IsValueType &&
                    prop.PropertyType != typeof(string))
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
    }
}

