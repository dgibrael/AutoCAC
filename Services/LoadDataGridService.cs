using AutoCAC.Utilities;
using Microsoft.EntityFrameworkCore; // Required for ToListAsync()
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Radzen;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Threading.Tasks;
namespace AutoCAC{
    public class LoadDataResult<T>
    {
        public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
        public int Count { get; set; }
    }

    public class LoadDataGridService
    {
        private readonly ParsingConfig _config = new() { RestrictOrderByToPropertyOrField = false };

        private string _lastFilter = null;
        private int _lastCount = 0;

        public async Task<LoadDataResult<T>> ApplyLoadData<T>(
            IQueryable<T> query,
            LoadDataArgs args,
            bool? shouldCount = null) // null = auto-detect
        {
            if (!string.IsNullOrEmpty(args.Filter))
            {
                query = query.Where(args.Filter);
            }

            shouldCount ??= _lastFilter != args.Filter;

            _lastFilter = args.Filter;

            if (shouldCount.Value)
            {
                _lastCount = await query.CountAsync();
            }

            if (!string.IsNullOrEmpty(args.OrderBy))
            {
                query = query.OrderBy(_config, args.OrderBy);
            }

            if (args.Skip.HasValue)
            {
                query = query.Skip(args.Skip.Value);
            }

            if (args.Top.HasValue)
            {
                query = query.Take(args.Top.Value);
            }

            var data = await query.ToListAsync();

            return new LoadDataResult<T>
            {
                Data = data,
                Count = _lastCount
            };
        }
        public static async Task GetColumnFilterDataAsync<T>(
            IQueryable<T> source,
            DataGridLoadColumnFilterDataEventArgs<T> args,
            int? defaultPageSize = 1000)
            where T : class, new()
        {
            var propName = args.Column.GetFilterProperty();

            var p = Expression.Parameter(typeof(T), "x");
            var propExpr = Expression.PropertyOrField(p, propName);
            var propType = propExpr.Type;

            // Static choices for bool/bool?; still page them so virtualization works nicely
            if (propType == typeof(bool))
            {
                args.Data = ObjectFactoryHelpers.CreateStubs<T>(propName, true, false);
                args.Count = 2;
                return;
            }

            if (propType == typeof(bool?))
            {
                args.Data = ObjectFactoryHelpers.CreateStubs<T>(propName, (bool?)true, (bool?)false, (bool?)null);
                args.Count = 3;
                return;
            }

            // Build q.Select(x => x.Prop)
            var selectLambda = Expression.Lambda(propExpr, p);
            var selectM = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), propType);
            var values = (IQueryable)selectM.Invoke(null, new object[] { source, selectLambda })!;

            // Cast to IQueryable<string?> when string to use EF.Functions.Like
            if (propType == typeof(string) && !string.IsNullOrWhiteSpace(args.Filter))
            {
                var strValues = (IQueryable<string?>)values;
                var like = $"%{args.Filter}%";
                values = strValues.Where(v => v != null && EF.Functions.Like(v, like));
            }

            // DISTINCT
            var distinctM = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.Distinct) && m.GetParameters().Length == 1)
                .MakeGenericMethod(propType);
            var distinct = (IQueryable)distinctM.Invoke(null, new[] { values })!;

            // ORDER BY x (when comparable) for stable UI
            distinct = TryOrderBySelf(distinct, propType);

            // COUNT
            var countAsyncM = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.CountAsync) && m.GetParameters().Length == 2)
                .MakeGenericMethod(propType);
            var countTask = (Task)countAsyncM.Invoke(null, new object?[] { distinct, default(System.Threading.CancellationToken) })!;
            await countTask.ConfigureAwait(false);
            var total = (int)countTask.GetType().GetProperty("Result")!.GetValue(countTask)!;

            // PAGE: Skip/Take
            var skipM = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.Skip) && m.GetParameters().Length == 2)
                .MakeGenericMethod(propType);
            var takeM = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.Take) && m.GetParameters().Length == 2)
                .MakeGenericMethod(propType);
            var paged = (IQueryable)takeM.Invoke(null, new object[] {
            skipM.Invoke(null, new object[] { distinct, args.Skip ?? 0 })!,
            defaultPageSize ?? args.Top ?? 100
        })!;

            // MATERIALIZE: ToListAsync<TProp>()
            var toListAsyncM = typeof(EntityFrameworkQueryableExtensions).GetMethods()
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
                .MakeGenericMethod(propType);
            var listTask = (Task)toListAsyncM.Invoke(null, new object[] { paged, default(System.Threading.CancellationToken) })!;
            await listTask.ConfigureAwait(false);
            var pageList = (System.Collections.IList)listTask.GetType().GetProperty("Result")!.GetValue(listTask)!;

            // Convert values -> List<T> stubs so Radzen can Select(propName)
            args.Data = ObjectFactoryHelpers.CreateStubs<T>(propName, pageList.Cast<object>().ToArray());
            args.Count = total;
        }

        private static IQueryable TryOrderBySelf(IQueryable source, Type propType)
        {
            if (typeof(IComparable).IsAssignableFrom(propType) ||
                propType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IComparable<>)))
            {
                var v = Expression.Parameter(propType, "v");
                var key = Expression.Lambda(v, v);
                var orderByM = typeof(Queryable).GetMethods()
                    .First(m => m.Name == nameof(Queryable.OrderBy) && m.GetParameters().Length == 2)
                    .MakeGenericMethod(propType, propType);
                return (IQueryable)orderByM.Invoke(null, new object[] { source, key })!;
            }
            return source; // leave unsorted if not comparable
        }

        public async Task GetColumnFilterDataAsync<T>(
            DataGridLoadColumnFilterDataEventArgs<T> args,
            string propertyName,
            params object[] filterChoices)
        {
            var list = ObjectFactoryHelpers.CreateStubs<T>(propertyName, filterChoices);
            args.Data = list;          // List<T> with the property present
            args.Count = list.Count;
        }
    }
}
