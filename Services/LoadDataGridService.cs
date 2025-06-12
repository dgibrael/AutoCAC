using Microsoft.EntityFrameworkCore; // Required for ToListAsync()
using Radzen;
using System.Collections.Generic;
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
        public async Task<List<T>> GetColumnFilterDataAsync<T>(
            IQueryable<T> source,
            DataGridLoadColumnFilterDataEventArgs<T> args)
        {
            var propertyName = args.Column.GetFilterProperty();

            var param = Expression.Parameter(typeof(T), "x");
            var propertyExpr = Expression.PropertyOrField(param, propertyName);
            var propertyToString = Expression.Call(propertyExpr, typeof(object).GetMethod("ToString")!);

            if (!string.IsNullOrEmpty(args.Filter))
            {
                var toLower = Expression.Call(propertyToString, typeof(string).GetMethod("ToLower", Type.EmptyTypes)!);
                var contains = Expression.Call(
                    toLower,
                    typeof(string).GetMethod("Contains", new[] { typeof(string) })!,
                    Expression.Constant(args.Filter.ToLower())
                );

                var lambda = Expression.Lambda<Func<T, bool>>(contains, param);
                source = source.Where(lambda);
            }

            // Build: source.GroupBy(x => x.Property).Select(g => g.First())
            var keySelector = Expression.Lambda(propertyExpr, param);
            var groupByMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == "GroupBy" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), propertyExpr.Type);

            var groupQuery = (IQueryable)groupByMethod.Invoke(null, new object[] { source, keySelector })!;

            var groupingType = typeof(IGrouping<,>).MakeGenericType(propertyExpr.Type, typeof(T));
            var firstMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "First" && m.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(T));

            var resultSelector = typeof(Queryable).GetMethods()
                .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                .MakeGenericMethod(groupingType, typeof(T));

            var groupParam = Expression.Parameter(groupingType, "g");
            var firstCall = Expression.Call(firstMethod, groupParam);
            var selector = Expression.Lambda(firstCall, groupParam);

            var finalQuery = (IQueryable<T>)resultSelector.Invoke(null, new object[] { groupQuery, selector })!;

            return await finalQuery.ToListAsync();
        }


    }
}
