using Microsoft.Extensions.Caching.Memory;

namespace AutoCAC.Services;

public sealed class CacheService
{
    private readonly IMemoryCache _cache;
    public CacheService(IMemoryCache cache) => _cache = cache;

    /// <summary>
    /// Get a cached value by key, or create it if missing.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// var users = await cache.GetOrCreateAsync(
    ///     "vpn-users",
    ///     () => adService.GetAdGroupMembersAsync("IHS VPN Users"),
    ///     durationMinutes: 10);
    /// </code>
    /// </remarks>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        int durationMinutes = 5)
    {
        if (_cache.TryGetValue(key, out T cached))
            return cached;

        var result = await factory();
        _cache.Set(key, result, TimeSpan.FromMinutes(durationMinutes));
        return result;
    }

    /// <summary>
    /// Get a cached value by <paramref name="key"/>, or run <paramref name="factory"/> to create it,
    /// storing the result for <paramref name="durationMinutes"/>.
    /// </summary>
    /// <remarks>
    /// Example usage:
    /// <code>
    /// var config = _cacheService.GetOrCreate(
    ///     "site-config",
    ///     () => _configLoader.Load(),
    ///     durationMinutes: 30);
    /// </code>
    /// </remarks>
    public T GetOrCreate<T>(
        string key,
        Func<T> factory,
        int durationMinutes = 5)
    {
        if (_cache.TryGetValue(key, out T cached))
            return cached;

        var result = factory();
        _cache.Set(key, result, TimeSpan.FromMinutes(durationMinutes));
        return result;
    }

    public void Invalidate(string key) => _cache.Remove(key);
}
