using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace AutoCAC.Extensions
{
    public static class IQueryableExtensions
    {
        private static readonly ParsingConfig Config = new()
        {
            RestrictOrderByToPropertyOrField = false
        };

        public static IQueryable<T> ApplyRadzenArgs<T>(
            this IQueryable<T> query,
            LoadDataArgs args,
            bool paging = false
            )
        {
            if (!string.IsNullOrEmpty(args.Filter))
            {
                query = query.Where(args.Filter);
            }

            if (paging)
            {
                if (args.Skip.HasValue)
                {
                    query = query.Skip(args.Skip.Value);
                }

                if (args.Top.HasValue)
                {
                    query = query.Take(args.Top.Value);
                }
            }

            if (!string.IsNullOrEmpty(args.OrderBy))
            {
                query = query.OrderBy(Config, args.OrderBy);
            }
            return query;
        }



        /// <summary>
        /// Convenience: Applies Filter/OrderBy (ignoring paging), materializes, converts to CSV, and downloads.
        /// </summary>
        public static async Task DownloadAsCsvAsync<T>(
            this IQueryable<T> query,
            LoadDataArgs args,
            IJSRuntime js,
            string fileName = "data.csv",
            bool paging = false,
            IEnumerable<string> includeProperties = null // ✅ optional columns
        )
            where T : class
        {
            var filteredSorted = query.ApplyRadzenArgs(args, paging);
            var list = await filteredSorted.AsNoTracking().ToListAsync();
            await list.DownloadAsCsvAsync(js, fileName, includeProperties: includeProperties);
        }
        public static IQueryable<T> QuickSearch<T>(
            this IQueryable<T> source,
            string searchText,
            params string[] columns)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(searchText) || columns is null || columns.Length == 0)
                return source;

            var keywords = searchText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (keywords.Length == 0)
                return source;

            var param = Expression.Parameter(typeof(T), "e");
            Expression final = null;

            // EF.Functions
            var efFunctionsProp = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);

            foreach (var kw in keywords)
            {
                Expression perKeywordOr = null;
                var pattern = Expression.Constant($"%{kw}%");

                foreach (var col in columns.Where(c => !string.IsNullOrWhiteSpace(c)))
                {
                    var prop = ResolvePropertyPath(param, col);  // your resolver
                    if (prop == null)
                        continue;

                    // Convert any type to string with null handling: (prop?.ToString()) ?? ""
                    var strExpr = ToSafeString(prop);

                    // EF.Functions.Like(strExpr, "%kw%")
                    var likeCall = Expression.Call(
                        typeof(DbFunctionsExtensions),
                        nameof(DbFunctionsExtensions.Like),
                        Type.EmptyTypes,
                        efFunctionsProp,
                        strExpr,
                        pattern);

                    perKeywordOr = perKeywordOr == null ? likeCall : Expression.OrElse(perKeywordOr, likeCall);
                }

                if (perKeywordOr == null)
                    continue;

                final = final == null ? perKeywordOr : Expression.AndAlso(final, perKeywordOr);
            }

            if (final == null)
                return source;

            var lambda = Expression.Lambda<Func<T, bool>>(final, param);
            return source.Where(lambda);
        }

        private static Expression ToSafeString(Expression prop)
        {
            // If Nullable<T>, use .Value before ToString()
            if (Nullable.GetUnderlyingType(prop.Type) is Type underlying)
            {
                prop = Expression.Property(prop, nameof(Nullable<int>.Value));
                // update type to underlying
            }

            // For non-string types call ToString(); for string keep as-is
            Expression asString = prop.Type == typeof(string)
                ? prop
                : Expression.Call(prop, nameof(object.ToString), Type.EmptyTypes);

            // Coalesce to empty string: (asString ?? "")
            return Expression.Coalesce(asString, Expression.Constant(string.Empty));
        }


        // Helper to support navigation properties like "Patient.Name" or "Address.City.Name"
        private static Expression ResolvePropertyPath(Expression param, string path)
        {
            Expression expr = param;
            foreach (var member in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                expr = Expression.PropertyOrField(expr!, member);
                if (expr == null)
                    return null;
            }
            return expr;
        }

    }
}
