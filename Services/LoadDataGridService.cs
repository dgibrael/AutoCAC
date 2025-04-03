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

            return await query.Skip(args.Skip ?? 0).Take(args.Top ?? 10).ToListAsync();
        }
    }
}
