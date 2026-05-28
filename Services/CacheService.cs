using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace AutoCAC.Services;

public sealed class CacheService : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly SqlWatcherFactory _sqlWatcherFactory;
    private readonly IDbContextFactory<MainContext> _dbContextFactory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly int AppSettingsCacheMinutes = 60;
    // "Users" -> watcher
    private readonly ConcurrentDictionary<string, SqlWatcher> _watchers =
        new(StringComparer.OrdinalIgnoreCase);

    // "Users" -> set of keys that should be invalidated when dbo.Users changes
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _tableKeys =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    public CacheService(
        IMemoryCache cache,
        SqlWatcherFactory sqlWatcherFactory,
        IDbContextFactory<MainContext> dbContextFactory)
    {
        _cache = cache;
        _sqlWatcherFactory = sqlWatcherFactory;
        _dbContextFactory = dbContextFactory;
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
        int durationMinutes = 20
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
        }
    }

    private void EnsureWatcher(string table)
    {
        _watchers.GetOrAdd(table, t =>
        {
            string query = $"SELECT COUNT_BIG(*) FROM [dbo].[{t}]";

            return _sqlWatcherFactory.Create(
                query,
                () =>
                {
                    InvalidateTable(t);
                    return Task.CompletedTask;
                });
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

    public async Task<T> GetAppSettingsGroupAsync<T>(string settingGroup)
    {
        string cacheKey = $"AppSettings:{settingGroup}:{typeof(T).FullName}";

        return await GetOrCreateAsync(
            cacheKey,
            async () =>
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();

                string json = await db.AppSettings
                    .AsNoTracking()
                    .Where(x => x.SettingGroup == settingGroup)
                    .Select(x => x.SettingValue)
                    .SingleAsync();

                T result = JsonSerializer.Deserialize<T>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return result;
            },
            watchDbTable: "AppSettings",
            durationMinutes: AppSettingsCacheMinutes);
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