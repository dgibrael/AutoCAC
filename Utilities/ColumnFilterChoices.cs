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
                var boollst = ObjectFactoryHelpers.CreateStubs<T>(propertyName, true, false);
                args.Data = boollst; args.Count = boollst.Count;
                _pageCache[pageKey] = boollst; _countCache[countKey] = boollst.Count;
                return;
            }
            if (propertyType == typeof(bool?))
            {
                var boollst = ObjectFactoryHelpers.CreateStubs<T>(propertyName, (bool?)true, (bool?)false, (bool?)null);
                args.Data = boollst; args.Count = boollst.Count;
                _pageCache[pageKey] = boollst; _countCache[countKey] = boollst.Count;
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

            // --- CHANGED SECTION: use Select + Distinct instead of GroupBy ---
            var keySelector = Expression.Lambda(propertyExpr, param);

            // query.Select(x => x.Prop)
            var selectM = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), propertyExpr.Type);
            var valuesQuery = (IQueryable)selectM.Invoke(null, new object[] { query, keySelector })!;

            // query.Select(...).Distinct()
            var distinctM = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == nameof(Queryable.Distinct) && m.GetParameters().Length == 1)
                .MakeGenericMethod(propertyExpr.Type);
            var distinctQuery = (IQueryable)distinctM.Invoke(null, new object[] { valuesQuery })!;

            // Materialize stub entities with the distinct values
            var prop = typeof(T).GetProperty(propertyName)!;
            var list = new List<T>();
            foreach (var val in await EntityFrameworkQueryableExtensions.ToListAsync((dynamic)distinctQuery))
            {
                var stub = Activator.CreateInstance<T>();
                prop.SetValue(stub, val);
                list.Add(stub);
            }
            var finalQuery = list.AsQueryable().AsNoTracking();
            // --- END CHANGED SECTION ---

            if (top == -1)
            {
                var all = finalQuery.ToList();
                args.Data = all; args.Count = all.Count;
                _pageCache[pageKey] = all; _countCache[countKey] = all.Count;
            }
            else
            {
                var pageList = finalQuery.Skip(skip).Take(top).ToList();

                int totalDistinct;
                if (!_countCache.TryGetValue(countKey, out totalDistinct))
                {
                    totalDistinct = finalQuery.Count();
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
