using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using AutoCAC.Models;
using AutoCAC.Utilities;
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
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            db.Attach(item);
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

