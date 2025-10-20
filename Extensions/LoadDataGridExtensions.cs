using Microsoft.EntityFrameworkCore;
using Radzen;
using System.Linq.Dynamic.Core;

namespace AutoCAC.Extensions
{
    public static class LoadDataGridExtensions
    {
        public class LoadDataResult<T>
        {
            public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
            public int Count { get; set; }
            public LoadDataArgs Args { get; set; } // last args
            public string QuickSearchString { get; set; }
            public string[] QuickSearchColumns { get; set; }
        }
        private static readonly ParsingConfig Dyn = new() { RestrictOrderByToPropertyOrField = true };

        public static async Task LoadDataGridAsync<T>(
            this IQueryable<T> source,
            LoadDataArgs args,
            LoadDataResult<T> state,
            string QuickSearchString,
            bool? shouldCount = null,
            bool ignoreFilter = false,
            CancellationToken ct = default)
            where T : class
        {
            var filtered = source;
            if (!string.IsNullOrWhiteSpace(QuickSearchString) && state.QuickSearchColumns?.Length > 0)
                filtered = filtered.QuickSearch<T>(QuickSearchString, state.QuickSearchColumns);
            if (!ignoreFilter && !string.IsNullOrWhiteSpace(args.Filter))
                filtered = filtered.Where(Dyn, args.Filter);

            var needCount = shouldCount ??
                            (
                                !string.Equals(state.Args?.Filter, args.Filter, StringComparison.Ordinal) ||
                                !string.Equals(state.QuickSearchString, QuickSearchString, StringComparison.OrdinalIgnoreCase)
                            );

            if (needCount)
                state.Count = await filtered.CountAsync(ct);

            var dataQ = filtered;
            if (!string.IsNullOrWhiteSpace(args.OrderBy))
                dataQ = dataQ.OrderBy(Dyn, args.OrderBy);
            if (args.Skip is int s) dataQ = dataQ.Skip(s);
            if (args.Top is int t) dataQ = dataQ.Take(t);

            state.Data = await dataQ.ToListAsync(ct);
            state.Args = args;
            state.QuickSearchString = QuickSearchString;
        }
    }
}
