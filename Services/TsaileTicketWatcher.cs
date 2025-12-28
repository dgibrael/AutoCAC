using AutoCAC.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AutoCAC.Services
{
    public class TsaileTicketWatcher : IDisposable
    {
        private readonly SqlWatcher _watcher;

        public event Action QueueChanged;

        public TsaileTicketWatcher(IDbContextFactory<mainContext> factory)
        {
            var query = @"SELECT COUNT_BIG(*) FROM dbo.tsaile_betterq;";

            _watcher = new SqlWatcher(factory, query, null);
            _watcher.Changed += OnSqlChanged;
        }

        private void OnSqlChanged()   // ✅ FIX
        {
            QueueChanged?.Invoke();
        }

        public void Dispose()
        {
            _watcher.Dispose();
        }
    }


}
