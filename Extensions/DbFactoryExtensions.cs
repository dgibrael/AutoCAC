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

    }
}

