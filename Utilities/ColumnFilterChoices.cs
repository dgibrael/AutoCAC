using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Radzen;

namespace AutoCAC.Utilities
{
    public class ColumnFilterChoices<T> where T : class
    {
        // page + count caches (unchanged)
        private readonly Dictionary<(string Property, string Filter, int Skip, int Top), IEnumerable<T>> _pageCache = new();
        private readonly Dictionary<(string Property, string Filter), int> _countCache = new();

        public async Task GetColumnFilterDataAsync(DataGridLoadColumnFilterDataEventArgs<T> args, IQueryable<T> query)
        {
            var propertyName = args.Column.GetFilterProperty();
            var currentFilter = args.Filter ?? string.Empty;

            var skip = args.Skip ?? 0;
            var top = args.Top ?? -1; // -1 = no virtualization

            // 👇 if non-virtualized, ignore filter in the cache key
            var filterForKey = top == -1 ? "" : currentFilter;

            var pageKey = (Property: propertyName, Filter: filterForKey, Skip: skip, Top: top);
            var countKey = (Property: propertyName, Filter: currentFilter);

            // ✅ Cached?
            if (_pageCache.TryGetValue(pageKey, out var cachedPage))
            {
                // If virtualization is off, apply the actual filter in-memory
                if (top == -1 && !string.IsNullOrEmpty(currentFilter))
                {
                    cachedPage = ApplyFilterInMemory(cachedPage, propertyName, currentFilter).ToList();
                }
                args.Data = cachedPage;
                if (_countCache.TryGetValue(countKey, out var cnt))
                {
                    args.Count = cnt;
                }
                else
                {
                    args.Count = cachedPage.Count();
                    _countCache[countKey] = args.Count;
                }
                return;
            }
            var param = Expression.Parameter(typeof(T), "x");
            var propertyExpr = Expression.PropertyOrField(param, propertyName);
            var propertyType = propertyExpr.Type;

            // bool / bool? -> no paging
            if (propertyType == typeof(bool))
            {
                var list = ObjectFactoryHelpers.CreateStubs<T>(propertyName, true, false);
                args.Data = list; args.Count = list.Count;
                _pageCache[pageKey] = list; _countCache[countKey] = list.Count;
                return;
            }
            if (propertyType == typeof(bool?))
            {
                var list = ObjectFactoryHelpers.CreateStubs<T>(propertyName, (bool?)true, (bool?)false, (bool?)null);
                args.Data = list; args.Count = list.Count;
                _pageCache[pageKey] = list; _countCache[countKey] = list.Count;
                return;
            }

            if (!string.IsNullOrEmpty(currentFilter))
            {
                var toString = Expression.Call(propertyExpr, typeof(object).GetMethod(nameof(object.ToString))!);
                var toLower = Expression.Call(toString, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                var contains = Expression.Call(
                    toLower,
                    typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!,
                    Expression.Constant(currentFilter.ToLower())
                );
                var lambda = Expression.Lambda<Func<T, bool>>(contains, param);
                query = query.Where(lambda);
            }

            // query.GroupBy(x => x.Prop).Select(g => g.First())
            var keySelector = Expression.Lambda(propertyExpr, param);
            var groupByM = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.GroupBy) && m.GetParameters().Length == 2)
                              .MakeGenericMethod(typeof(T), propertyExpr.Type);
            var groupQuery = (IQueryable)groupByM.Invoke(null, new object[] { query, keySelector })!;

            var groupingType = typeof(IGrouping<,>).MakeGenericType(propertyExpr.Type, typeof(T));
            var firstM = typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.First) && m.GetParameters().Length == 1)
                              .MakeGenericMethod(typeof(T));
            var selectM = typeof(Queryable).GetMethods().First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2)
                              .MakeGenericMethod(groupingType, typeof(T));
            var gParam = Expression.Parameter(groupingType, "g");
            var firstCall = Expression.Call(firstM, gParam);
            var selector = Expression.Lambda(firstCall, gParam);
            var finalQuery = (IQueryable<T>)selectM.Invoke(null, new object[] { groupQuery, selector })!;

            if (top == -1)
            {
                var list = await finalQuery.AsNoTracking().ToListAsync();
                args.Data = list; args.Count = list.Count;
                _pageCache[pageKey] = list; _countCache[countKey] = list.Count;
            }
            else
            {
                var paged = finalQuery.Skip(skip).Take(top);
                var pageList = await paged.AsNoTracking().ToListAsync();

                int totalDistinct;
                if (!_countCache.TryGetValue(countKey, out totalDistinct))
                {
                    totalDistinct = await finalQuery.CountAsync();
                    _countCache[countKey] = totalDistinct;
                }

                args.Data = pageList; args.Count = totalDistinct;
                _pageCache[pageKey] = pageList;
            }
        }
        private static IEnumerable<T> ApplyFilterInMemory(IEnumerable<T> items, string propertyName, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return items;

            var prop = typeof(T).GetProperty(propertyName);
            if (prop == null) return items;

            return items.Where(i =>
            {
                var s = prop.GetValue(i)?.ToString() ?? string.Empty;
                return s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

    }
}
