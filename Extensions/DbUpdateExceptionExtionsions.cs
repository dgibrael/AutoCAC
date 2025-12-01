using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace AutoCAC.Extensions
{
    public static class DbUpdateExceptionExtensions
    {
        public static bool IsDuplicateKey(this DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sql)
                return sql.Number == 2601 || sql.Number == 2627;

            return false;
        }
    }
}

