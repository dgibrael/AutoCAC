using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace AutoCAC.Services;

public sealed class CacheService : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly string _connectionString;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    // "Users" -> watcher
    private readonly ConcurrentDictionary<string, SqlWatcher> _watchers =
        new(StringComparer.OrdinalIgnoreCase);

    // "Users" -> set of keys that should be invalidated when dbo.Users changes
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tableKeys =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    public CacheService(IMemoryCache cache, IConfiguration configuration)
    {
        _cache = cache;
        _connectionString = configuration.GetConnectionString("mainConnection")
            ?? throw new InvalidOperationException("Missing connection string 'mainConnection'.");
    }

    public void Invalidate(string key) => _cache.Remove(key);

    public int InvalidateTable(string table)
    {
        if (!_tableKeys.TryGetValue(table, out var set))
            return 0;

        var keys = set.Keys.ToArray();
        foreach (var k in keys)
            _cache.Remove(k); // triggers eviction callback => untrack

        return keys.Length;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        string watchDbTable = null,
        int durationMinutes = 10
        )
    {
        if (_cache.TryGetValue(key, out T cached))
            return cached;

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(key, out cached))
                return cached;

            MemoryCacheEntryOptions options = null;

            if (!string.IsNullOrWhiteSpace(watchDbTable))
            {
                EnsureWatcher(watchDbTable);

                options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(durationMinutes)
                };

                TrackKey(watchDbTable, key, options);
            }

            var result = await factory().ConfigureAwait(false);

            if (options != null)
                _cache.Set(key, result, options);
            else
                _cache.Set(key, result, TimeSpan.FromMinutes(durationMinutes));

            return result;
        }
        finally
        {
            gate.Release();
            if (_locks.TryRemove(key, out var removed))
                removed.Dispose();
        }
    }

    private void EnsureWatcher(string table)
    {
        _watchers.GetOrAdd(table, t =>
        {
            var query = $"SELECT COUNT_BIG(*) FROM [dbo].[{t}]";
            var watcher = new SqlWatcher(_connectionString, query);
            watcher.ChangedAsync += () =>
            {
                InvalidateTable(t);
                return Task.CompletedTask;
            };
            return watcher;
        });
    }

    private void TrackKey(string table, string key, MemoryCacheEntryOptions options)
    {
        var set = _tableKeys.GetOrAdd(
            table,
            _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));

        set[key] = 0;

        options.RegisterPostEvictionCallback((k, v, reason, state) =>
        {
            if (k is not string s) return;
            UntrackKey(table, s);
        });
    }

    private void UntrackKey(string table, string key)
    {
        if (_tableKeys.TryGetValue(table, out var set))
        {
            set.TryRemove(key, out _);

            if (set.IsEmpty)
            {
                _tableKeys.TryRemove(table, out _);

                if (_watchers.TryRemove(table, out var watcher))
                {
                    try { watcher.Dispose(); } catch { }
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var w in _watchers.Values)
        {
            try { w.Dispose(); } catch { }
        }

        _watchers.Clear();
    }
}