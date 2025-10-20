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
    public class LoadDataServiceResult<T>
    {
        public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
        public int Count { get; set; }
    }

    public class LoadDataGridService
    {
        private readonly ParsingConfig _config = new() { RestrictOrderByToPropertyOrField = false };

        private string _lastFilter = null;
        private int _lastCount = 0;

        public async Task<LoadDataServiceResult<T>> ApplyLoadData<T>(
            IQueryable<T> query,
            LoadDataArgs args,
            bool? shouldCount = null,
            bool ignoreFilter = false
            ) // null = auto-detect
        {
            if (!string.IsNullOrEmpty(args.Filter) && !ignoreFilter)
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

            return new LoadDataServiceResult<T>
            {
                Data = data,
                Count = _lastCount
            };
        }

    }
}
