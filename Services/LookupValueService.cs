using Microsoft.EntityFrameworkCore;
using AutoCAC.Models;
using System.Collections.Concurrent;

namespace AutoCAC.Services;

public class LookupValueService
{
    public enum CommonOptionSet
    {
        TsaileBatchTo
    }

    private readonly IDbContextFactory<mainContext> _dbFactory;

    // Cache the completed or in-flight load per option set.
    private readonly ConcurrentDictionary<string, Lazy<Task<LookupValue[]>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public LookupValueService(IDbContextFactory<mainContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // You can return LookupValue[] directly if you want the fastest/cleanest.
    public Task<LookupValue[]> GetOptionSetAsync(string optionSet)
    {
        if (string.IsNullOrWhiteSpace(optionSet))
            return Task.FromResult(Array.Empty<LookupValue>());

        var lazy = _cache.GetOrAdd(optionSet, key =>
            // Don't tie cached load to a request CancellationToken (prevents caching canceled tasks)
            new Lazy<Task<LookupValue[]>>(() => LoadOptionSetAsync(key, CancellationToken.None)));

        return lazy.Value;
    }

    public Task<LookupValue[]> GetOptionSetAsync(CommonOptionSet optionSet)
        => GetOptionSetAsync(optionSet.ToString());

    public async Task<string[]> GetValuesAsync(string optionSet)
    {
        var rows = await GetOptionSetAsync(optionSet);
        return rows.Select(x => x.Value).ToArray();
    }

    public Task<string[]> GetValuesAsync(CommonOptionSet optionSet)
        => GetValuesAsync(optionSet.ToString());

    public void Invalidate(string optionSet)
    {
        if (string.IsNullOrWhiteSpace(optionSet))
            return;

        _cache.TryRemove(optionSet, out _);
    }

    public void Invalidate(CommonOptionSet optionSet)
        => Invalidate(optionSet.ToString());

    public void InvalidateAll()
        => _cache.Clear();

    private async Task<LookupValue[]> LoadOptionSetAsync(string optionSet, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Set<LookupValue>()
            .AsNoTracking()
            .Where(x => x.IsActive && x.OptionSet == optionSet)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayText)
            .ToListAsync(ct);

        return rows.ToArray();
    }
}
