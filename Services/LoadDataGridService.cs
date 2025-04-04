using System.Linq.Dynamic.Core;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore; // Required for ToListAsync()
using Radzen;

namespace AutoCAC{
    public class LoadDataGridService
    {
        private readonly ParsingConfig _config = new() { RestrictOrderByToPropertyOrField = false };

        public async Task<IEnumerable<T>> ApplyLoadData<T>(IQueryable<T> query, LoadDataArgs args)
        {
            if (!string.IsNullOrEmpty(args.Filter))
            {
                query = query.Where(args.Filter);
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

            return await query.ToListAsync();
        }
    }
}
