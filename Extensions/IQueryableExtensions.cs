using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using System.Linq.Dynamic.Core;
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
    }
}
