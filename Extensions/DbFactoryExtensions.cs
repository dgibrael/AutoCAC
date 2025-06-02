using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using AutoCAC.Models;

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

        public static IQueryable<T> QueryFromSql<T>(
            this IDbContextFactory<mainContext> factory,
            string sql,
            params object[] parameters) where T : class
        {
            var context = factory.CreateDbContext();
            return context.Set<T>().FromSqlRaw(sql, parameters).AsNoTracking();
        }

    }
}

