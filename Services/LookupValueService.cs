using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Immutable;
using System.Text.Json;

namespace AutoCAC.Services;

public class LookupValueService
{
    public record ValueWithOption(string Value, string OptionValue);
    public enum CommonOptionSet
    {
        TsaileBatchTo
    }

    private readonly IDbContextFactory<mainContext> _dbFactory;

    // optionSet -> rows (immutable, safe to hand out)
    private ImmutableDictionary<string, ImmutableArray<LookupValue>> _cache
        = ImmutableDictionary.Create<string, ImmutableArray<LookupValue>>(StringComparer.OrdinalIgnoreCase);

    // Reuse the same empty list instance
    private static readonly ImmutableArray<LookupValue> EmptyLookupValues = ImmutableArray<LookupValue>.Empty;
    private static readonly ImmutableArray<string> EmptyStrings = ImmutableArray<string>.Empty;

    public LookupValueService(IDbContextFactory<mainContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // Call once at startup
    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Set<LookupValue>()
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.OptionSet)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayText)
            .ToListAsync(ct);

        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<LookupValue>>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in rows.GroupBy(x => x.OptionSet, StringComparer.OrdinalIgnoreCase))
            builder[group.Key] = group.ToImmutableArray();

        _cache = builder.ToImmutable();
    }

    // Admin-only: reload one option set after update
    public async Task ReloadOptionSetAsync(string optionSet, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(optionSet))
            return;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.Set<LookupValue>()
            .AsNoTracking()
            .Where(x => x.IsActive && x.OptionSet == optionSet)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayText)
            .ToListAsync(ct);

        _cache = _cache.SetItem(optionSet, rows.ToImmutableArray());
    }

    public IReadOnlyDictionary<string, ImmutableArray<LookupValue>> All => _cache;

    // Fast sync read
    public IReadOnlyList<LookupValue> GetOptionSet(string optionSet)
    {
        if (string.IsNullOrWhiteSpace(optionSet))
            return EmptyLookupValues;

        if (_cache.TryGetValue(optionSet, out var list))
            return list;

        return EmptyLookupValues;
    }

    public IReadOnlyList<LookupValue> GetOptionSet(CommonOptionSet optionSet)
        => GetOptionSet(optionSet.ToString());

    public IReadOnlyList<string> GetValues(string optionSet)
    {
        var rows = GetOptionSet(optionSet);
        if (rows.Count == 0)
            return EmptyStrings;

        // Still allocates a new list of strings; if you want zero allocations,
        // return LookupValue list and let callers select Value as needed.
        return rows.Select(x => x.Value).ToImmutableArray();
    }

    public IReadOnlyList<string> GetValues(CommonOptionSet optionSet)
        => GetValues(optionSet.ToString());

    public IReadOnlyList<ValueWithOption> GetValuesWithOption(string optionSet, string optionKey)
    {
        if (string.IsNullOrWhiteSpace(optionSet) || string.IsNullOrWhiteSpace(optionKey))
            return Array.Empty<ValueWithOption>();

        var rows = GetOptionSet(optionSet);
        if (rows.Count == 0)
            return Array.Empty<ValueWithOption>();

        var results = new List<ValueWithOption>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];

            var optVal = "";

            if (!string.IsNullOrWhiteSpace(r.Options))
                TryGetOptionValue(r.Options, optionKey, out optVal);

            results.Add(new ValueWithOption(r.Value, optVal));
        }

        return results;
    }

    private static bool TryGetOptionValue(string optionsJson, string optionKey, out string optionValue)
    {
        optionValue = "";

        try
        {
            using var doc = JsonDocument.Parse(optionsJson);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (!doc.RootElement.TryGetProperty(optionKey, out var prop))
                return false;

            optionValue = prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString() ?? "",
                JsonValueKind.Number => prop.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => prop.GetRawText() // object/array: return raw JSON
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
