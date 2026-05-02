// Utilities/DataListHelper.cs
using AutoCAC.Extensions;
using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using System.Linq.Dynamic.Core;

namespace AutoCAC;

public sealed class DataListHelper<T> where T : class
{
    private IDbContextFactory<MainContext> _db;

    private static readonly Func<MainContext, IQueryable<T>> DefaultQuery =
        db => db.Set<T>().AsQueryable().AsNoTracking();

    private readonly ParsingConfig _config = new() { RestrictOrderByToPropertyOrField = false };
    public bool IsInitialized { get; private set; }

    private Func<Task> _reloadAsync;
    private bool _reloadPending;

    Func<NotificationMessage, Task> _notifyAsync;
    public void Initialize(IDbContextFactory<MainContext> dbFactory, Func<Task> reloadAsync, Func<NotificationMessage, Task> notifyAsync)
    {
        // always refresh these (safe + predictable)
        _db = dbFactory;

        if (!IsInitialized)
        {
            // one-time init
            SearchText = InitialSearchText ?? string.Empty;
            ShouldCount = true;
            IsInitialized = true;
        }
        _notifyAsync = notifyAsync;
        // attach/replace reload handler (single-owner model)
        _reloadAsync = reloadAsync;

        // if a reload was requested before handler existed, run once now
        if (_reloadPending)
        {
            _reloadPending = false;
            _ = SafeReloadAsync();
        }
    }

    public void DetachReloadHandler()
    {
        _reloadAsync = null;
    }

    private CancellationTokenSource _loadCts;
    private CancellationToken _currentLoadToken;
    private int _loadVersion;
    private int _activeVersion;
    public Task ReloadAsync(CancellationToken ct = default, bool ClearCache = true)
    {
        ShouldCount = true;
        if (UseClientSideData && ClearCache)
        {
            _cache = null;
        }
        _activeVersion = Interlocked.Increment(ref _loadVersion);
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _currentLoadToken = _loadCts.Token;

        if (_reloadAsync is null)
        {
            _reloadPending = true;
            return Task.CompletedTask;
        }

        return SafeReloadAsync();
    }

    private async Task SafeReloadAsync()
    {
        try
        {
            await _reloadAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_notifyAsync != null)
            {
                await _notifyAsync(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Reload failed",
                    Detail = ex.Message,
                    Duration = 4000
                });
            }
        }
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized || _db is null)
            throw new InvalidOperationException("DataListHelper is not initialized. Call Helper.Initialize(dbFactory, username, reloadAsync) first.");
    }
    public string DataListName { get; set; }

    public string AddButtonUrl { get; set; }
    public bool AddButtonDefault { get; set; } = false;

    public bool ActionColumnDefault { get; set; } = false;
    public string[] SearchColumns { get; set; }
    public Func<MainContext, IQueryable<T>> QueryFactory { get; set; }

    public bool UseClientSideData { get; set; } = false;
    public bool IgnoreFilter { get; set; } = false;
    public string InitialSearchText { get; set; } = "";

    public Dictionary<string, IEnumerable<object>> CustomChoices { get; set; }

    public string PrintHeader { get; set; }
    public bool AllowSorting { get; set; } = true;
    public bool ShowDownloadButton { get; set; } = true;
    private string _lastFilter;
    private string _lastSearchText;

    public string SearchText { get; set; }
    public bool? ShouldCount { get; set; }
    public Func<MainContext, IQueryable<T>> LastBuilder { get; private set; }

    public IEnumerable<T> Data { get; set; }
    public int Count { get; set; }
    private List<T> _cache;

    public string PageName =>
        !string.IsNullOrWhiteSpace(DataListName)
            ? DataListName!
            : typeof(T).Name;

    // Parameterless constructor so DataListVanilla can default to new()
    public DataListHelper()
    {
        ShouldCount = true;
    }

    public async Task SetQueryFactoryAsync(
        Func<MainContext, IQueryable<T>> queryFactory,
        bool clearClientCache = true,
        bool requestReload = true,
        CancellationToken ct = default)
    {
        QueryFactory = queryFactory;

        // invalidate derived state
        ShouldCount = true;
        _lastFilter = null;
        _lastSearchText = null;
        LastBuilder = null;

        if (clearClientCache)
            _cache = null;

        if (requestReload)
            await ReloadAsync(ct);
    }

    public async Task SetQuickFilterAsync(string text = null)
    {
        SearchText = text ?? string.Empty;
        await ReloadAsync(ClearCache: false);
    }

    public async Task LoadAsync(LoadDataArgs args, CancellationToken ct = default)
    {
        EnsureInitialized();

        if (ct == default && _currentLoadToken != default)
            ct = _currentLoadToken;

        var versionAtStart = _activeVersion;

        try
        {
            if (UseClientSideData)
            {
                await LoadClientSideAsync(args, ct);
                return;
            }

            await using var ctx = await _db.CreateDbContextAsync(ct);

            var source = QueryFactory ?? DefaultQuery;
            IQueryable<T> query = source(ctx).AsNoTracking();

            if (!string.IsNullOrEmpty(args.Filter) &&
                !args.Filter.Replace(" ", "").Equals("0=1", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(args.Filter);
            }

            if (!string.IsNullOrWhiteSpace(SearchText) && SearchColumns != null && SearchColumns.Length > 0)
                query = query.QuickSearch(SearchText, SearchColumns);

            ShouldCount ??= _lastFilter != args.Filter || _lastSearchText != SearchText;
            if (ShouldCount == true)
            {
                var newCount = await query.CountAsync(ct);

                // stale? drop it
                if (versionAtStart == _activeVersion)
                {
                    Count = newCount;
                    _lastFilter = args.Filter;
                    _lastSearchText = SearchText;
                }
                else
                {
                    return; // ok to return here because we're in try/finally below
                }
            }

            if (!string.IsNullOrEmpty(args.OrderBy))
                query = query.OrderBy(_config, args.OrderBy);

            if (args.Skip.HasValue) query = query.Skip(args.Skip.Value);
            if (args.Top.HasValue) query = query.Take(args.Top.Value);

            var list = await query.ToListAsync(ct);

            if (versionAtStart == _activeVersion)
            {
                Data = list;

                LastBuilder = c =>
                {
                    var q = (QueryFactory ?? DefaultQuery)(c).AsNoTracking();

                    if (!string.IsNullOrWhiteSpace(SearchText) && SearchColumns != null && SearchColumns.Length > 0)
                        q = q.QuickSearch(SearchText, SearchColumns);

                    if (!string.IsNullOrEmpty(args.Filter) && !IgnoreFilter)
                        q = q.Where(args.Filter);

                    return q;
                };
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            // Always clear this so future loads recompute correctly.
            // (If you want to keep it non-null on cancellation, you can, but current code clears it.)
            ShouldCount = null;
        }
    }

    public async Task DownloadCsvAsync(IJSRuntime js, int hardLimit = 100000, CancellationToken ct = default)
    {
        EnsureInitialized();

        if (Count > hardLimit)
            throw new InvalidOperationException("Data set too large to download. Filter first.");

        await using var ctx = await _db.CreateDbContextAsync(ct);
        var query = (LastBuilder ?? (QueryFactory ?? DefaultQuery))(ctx);
        await query.DownloadAsCsvAsync(new LoadDataArgs(), js);
    }
    private async Task LoadClientSideAsync(LoadDataArgs args, CancellationToken ct)
    {
        EnsureInitialized();
        // Load full data set if we don't have it yet
        if (_cache == null)
        {
            await using var ctx = await _db.CreateDbContextAsync(ct);
            _cache = await (QueryFactory ?? DefaultQuery)(ctx).AsNoTracking().ToListAsync(ct);
        }

        IEnumerable<T> query = _cache;

        // Apply search
        if (!string.IsNullOrWhiteSpace(SearchText) && SearchColumns != null && SearchColumns.Length > 0)
            query = query.AsQueryable().QuickSearch(SearchText, SearchColumns);
        var normalizedFilter = NormalizeClientSideFilter(args.Filter);
        // Apply dynamic filter expression
        if (!string.IsNullOrEmpty(normalizedFilter) &&
            !normalizedFilter.Replace(" ", "").Equals("0=1", StringComparison.OrdinalIgnoreCase))
        {
            query = query.AsQueryable().Where(normalizedFilter);
        }

        // Count after filtering
        Count = query.Count();

        // Sorting
        if (!string.IsNullOrEmpty(args.OrderBy))
            query = query.AsQueryable().OrderBy(_config, args.OrderBy);

        // Paging (in memory)
        if (args.Skip.HasValue) query = query.Skip(args.Skip.Value);
        if (args.Top.HasValue) query = query.Take(args.Top.Value);

        Data = query.ToList();

        LastBuilder = c =>
        {
            IQueryable<T> q = _cache.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchText) && SearchColumns != null && SearchColumns.Length > 0)
                q = q.QuickSearch(SearchText, SearchColumns);

            if (!string.IsNullOrEmpty(normalizedFilter) && !IgnoreFilter)
                q = q.Where(normalizedFilter);

            return q;
        };

        ShouldCount = null;
    }

    private static string NormalizeClientSideFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return filter;

        // Pass 1: append .ToLowerInvariant() after each *string literal* (handles escaped quotes).
        var sb = new System.Text.StringBuilder(filter.Length + 64);

        for (int i = 0; i < filter.Length; i++)
        {
            var ch = filter[i];
            sb.Append(ch);

            if (ch != '"')
                continue;

            // We are inside a string literal, copy until closing quote (respecting escapes).
            i++;
            for (; i < filter.Length; i++)
            {
                sb.Append(filter[i]);

                if (filter[i] == '\\' && i + 1 < filter.Length)
                {
                    // escape sequence, copy next char too
                    i++;
                    sb.Append(filter[i]);
                    continue;
                }

                if (filter[i] == '"')
                {
                    // closing quote
                    sb.Append(".ToLowerInvariant()");
                    break;
                }
            }
        }

        var rewritten = sb.ToString();

        // Pass 2: move ToLowerInvariant from the "" literal to the whole coalesced expression.
        // Turns: (x.Prop ?? "".ToLowerInvariant()) into (x.Prop ?? "").ToLowerInvariant()
        rewritten = rewritten.Replace(@"?? """".ToLowerInvariant())", @"?? """").ToLowerInvariant()");

        return rewritten;
    }
}
