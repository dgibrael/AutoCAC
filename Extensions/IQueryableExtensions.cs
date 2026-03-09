using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.JSInterop;
using Radzen;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

namespace AutoCAC.Extensions;

public static class IQueryableExtensions
{

    extension<T>(IQueryable<T> query)
    {
        // Usage: var key = query.CacheKey;
        public string CacheKey
        {
            get
            {
                if (query == null) throw new ArgumentNullException(nameof(query));

                string identity;
                try
                {
                    // EF Core provider: includes WHERE/ORDER/OFFSET-FETCH, etc.
                    identity = query.ToQueryString();
                }
                catch
                {
                    // Non-EF providers fallback
                    identity = query.Expression.ToString();
                }

                // Reduce collision risk across different element types
                identity = typeof(T).FullName + "|" + identity;

                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
                return Convert.ToHexString(bytes);
            }
        }
    }

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
        IEnumerable<string> includeProperties = null,
        CancellationToken ct = default
    )
        where T : class
    {
        var filteredSorted = query.ApplyRadzenArgs(args, paging);

        // If it's EF-backed, we can (and should) do EF async + AsNoTracking.
        // If it's LINQ-to-Objects (client-side cache), EF extensions will throw, so fall back to sync.
        List<T> list;

        if (filteredSorted.Provider is IAsyncQueryProvider)
        {
            // AsNoTracking() is safe here and avoids change tracker overhead.
            list = await filteredSorted.AsNoTracking().ToListAsync(ct);
        }
        else
        {
            // LINQ-to-Objects (e.g., _cache.AsQueryable()) -> sync enumeration
            // CancellationToken can't be honored mid-enumeration without custom logic.
            list = filteredSorted.ToList();
        }

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

        // If this is an EF query provider, keep the EF.Like translation.
        // Otherwise (LINQ-to-Objects), use IndexOf(..., OrdinalIgnoreCase).
        var isEf = source.Provider is IAsyncQueryProvider;

        var param = Expression.Parameter(typeof(T), "e");
        Expression final = null;

        // EF.Functions (only used for EF path)
        var efFunctionsProp = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);

        // string.IndexOf(string, StringComparison)
        var indexOfMethod = typeof(string).GetMethod(nameof(string.IndexOf), new[] { typeof(string), typeof(StringComparison) });
        var ordIgnoreCase = Expression.Constant(StringComparison.OrdinalIgnoreCase);

        foreach (var kw in keywords)
        {
            Expression perKeywordOr = null;

            // For EF Like: "%kw%"
            var likePattern = Expression.Constant($"%{kw}%");
            // For IndexOf: "kw"
            var kwConst = Expression.Constant(kw);

            foreach (var col in columns.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                var prop = ResolvePropertyPath(param, col);
                if (prop == null)
                    continue;

                // (prop?.ToString()) ?? ""
                var strExpr = ToSafeString(prop);

                Expression matchExpr;

                if (isEf)
                {
                    // EF.Functions.Like(strExpr, "%kw%")
                    matchExpr = Expression.Call(
                        typeof(DbFunctionsExtensions),
                        nameof(DbFunctionsExtensions.Like),
                        Type.EmptyTypes,
                        efFunctionsProp,
                        strExpr,
                        likePattern);
                }
                else
                {
                    // strExpr.IndexOf(kw, OrdinalIgnoreCase) >= 0
                    var indexOfCall = Expression.Call(strExpr, indexOfMethod!, kwConst, ordIgnoreCase);
                    matchExpr = Expression.GreaterThanOrEqual(indexOfCall, Expression.Constant(0));
                }

                perKeywordOr = perKeywordOr == null ? matchExpr : Expression.OrElse(perKeywordOr, matchExpr);
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
