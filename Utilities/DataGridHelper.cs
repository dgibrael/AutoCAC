// Utilities/DataGridHelper.cs
using AutoCAC.Extensions;
using AutoCAC.Models;
using AutoCAC.Utilities;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text.Json;

namespace AutoCAC;

public sealed class DataGridHelper<T> where T : class
{
    private IDbContextFactory<mainContext> _db;

    private static readonly Func<mainContext, IQueryable<T>> DefaultQuery =
        db => db.Set<T>().AsQueryable().AsNoTracking();

    private readonly ParsingConfig _config = new() { RestrictOrderByToPropertyOrField = false };
    private ColumnFilterChoices<T> _filterChoices;

    // -------------------------
    // Init + reload wiring
    // -------------------------
    public bool IsInitialized { get; private set; }

    private Func<Task> _reloadAsync;
    private bool _reloadPending;

    public void Initialize(IDbContextFactory<mainContext> dbFactory, string username, Func<Task> reloadAsync)
    {
        // always refresh these (safe + predictable)
        _db = dbFactory;
        UserName = username;

        if (!IsInitialized)
        {
            // one-time init
            SearchText = InitialSearchText ?? string.Empty;
            ShouldCount = true;
            IsInitialized = true;
        }

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

    public void Reload()
        => _ = ReloadAsync();

    public Task ReloadAsync()
    {
        ShouldCount = true;
        if (UseClientSideData)
            _cache = null;
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
        catch
        {
            // swallow or log
        }
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized || _db is null)
            throw new InvalidOperationException("DataGridHelper is not initialized. Call Helper.Initialize(dbFactory, username, reloadAsync) first.");
    }

    // -------------------------
    // Former DataGridVanilla parameters (now Helper properties)
    // -------------------------
    public string DataGridName { get; set; }

    public string AddButtonUrl { get; set; }
    public bool AddButtonDefault { get; set; } = false;

    public bool ActionColumnDefault { get; set; } = false;

    public IEnumerable<string> ExcludeColumns { get; set; } = new List<string>();
    public IEnumerable<string> IncludeColumns { get; set; }

    public string[] SearchColumns { get; set; }
    public Func<mainContext, IQueryable<T>> QueryFactory { get; set; }

    public bool UseClientSideData { get; set; } = false;
    public bool IgnoreFilter { get; set; } = false;

    public DataGridTemplate InitialTemplate { get; set; }
    public string InitialSearchText { get; set; } = "";

    public Dictionary<string, IEnumerable<object>> CustomChoices { get; set; }

    public string PrintHeader { get; set; }

    public bool AllowGrouping { get; set; } = true;
    public bool AllowSorting { get; set; } = true;
    public bool AllowFiltering { get; set; } = true;
    public bool AllowColumnReorder { get; set; } = true;
    public bool AllowColumnPicking { get; set; } = true;

    // -------------------------
    // Existing helper state/output (kept close to your current code)
    // -------------------------
    private string _lastFilter;
    private string _lastSearchText;

    public string SearchText { get; set; }
    public bool? ShouldCount { get; set; }
    public Func<mainContext, IQueryable<T>> LastBuilder { get; private set; }

    public IEnumerable<T> Data { get; set; }
    public int Count { get; set; }

    public DataGridSettings Settings { get; set; }
    public PivotGridSettings PivotSettings { get; set; }

    public DataGridTemplate LoadedTemplate { get; set; }
    public string UserName { get; set; }

    private List<T> _cache;
    public bool IsPivotTable { get; set; }

    public string PageName =>
        !string.IsNullOrWhiteSpace(DataGridName)
            ? DataGridName!
            : typeof(T).Name;

    // Parameterless constructor so DataGridVanilla can default to new()
    public DataGridHelper()
    {
        ShouldCount = true;
    }

    // -------------------------
    // Query mutation API (caller changes QueryFactory, helper handles invalidation + reload)
    // -------------------------
    public void SetQueryFactory(Func<mainContext, IQueryable<T>> queryFactory, bool clearClientCache = true, bool requestReload = true)
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
            Reload();
    }

    public async Task SetQuickFilterAsync(string text = null)
    {
        SearchText = text ?? string.Empty;
        await ReloadAsync();
    }

    // -------------------------
    // Data loading
    // -------------------------
    public async Task LoadAsync(LoadDataArgs args, CancellationToken ct = default)
    {
        EnsureInitialized();

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
        if (ShouldCount.Value)
        {
            Count = await query.CountAsync(ct);
            _lastFilter = args.Filter;
            _lastSearchText = SearchText;
        }

        if (!string.IsNullOrEmpty(args.OrderBy))
            query = query.OrderBy(_config, args.OrderBy);

        if (args.Skip.HasValue) query = query.Skip(args.Skip.Value);
        if (args.Top.HasValue) query = query.Take(args.Top.Value);

        Data = await query.ToListAsync(ct);

        LastBuilder = c =>
        {
            var q = (QueryFactory ?? DefaultQuery)(c).AsNoTracking();

            if (!string.IsNullOrWhiteSpace(SearchText) && SearchColumns != null && SearchColumns.Length > 0)
                q = q.QuickSearch(SearchText, SearchColumns);

            if (!string.IsNullOrEmpty(args.Filter) && !IgnoreFilter)
                q = q.Where(args.Filter);

            return q;
        };

        ShouldCount = null;
    }

    public async Task LoadColumnFilterDataAsync(DataGridLoadColumnFilterDataEventArgs<T> args)
    {
        EnsureInitialized();

        args.Top = null;
        args.Skip = null;

        var propertyName = args.Column.GetFilterProperty();

        // CUSTOM CHOICES
        if (CustomChoices != null && CustomChoices.TryGetValue(propertyName, out var values))
        {
            args.Data = ObjectFactoryHelpers.CreateStubs<T>(propertyName, values);
            args.Count = values.Count();
            return; // IMPORTANT: bypass DB
        }

        await using var ctx = await _db.CreateDbContextAsync();
        var query = (QueryFactory ?? DefaultQuery)(ctx).AsNoTracking();

        _filterChoices ??= new ColumnFilterChoices<T>();
        await _filterChoices.GetColumnFilterDataAsync(args, query);
    }

    // -------------------------
    // Export
    // -------------------------
    public async Task DownloadCsvAsync(IJSRuntime js, int hardLimit = 100000, CancellationToken ct = default)
    {
        EnsureInitialized();

        if (Count > hardLimit)
            throw new InvalidOperationException("Data set too large to download. Filter first.");

        await using var ctx = await _db.CreateDbContextAsync(ct);
        var query = (LastBuilder ?? (QueryFactory ?? DefaultQuery))(ctx);

        var visibleProps = Settings != null && !IsPivotTable
            ? Settings.Columns
                .Where(c => c.Visible)
                .Select(c => c.Property)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .ToList()
            : null;

        await query.DownloadAsCsvAsync(new LoadDataArgs(), js, includeProperties: visibleProps);
    }

    // -------------------------
    // Templates
    // -------------------------
    public async Task<List<DataGridTemplate>> GetTemplatesAsync()
    {
        EnsureInitialized();

        await using var db = await _db.CreateDbContextAsync();
        return await db.DataGridTemplates
            .AsNoTracking()
            .Where(t => t.DataGridName == PageName && t.CreatedBy == UserName)
            .ToListAsync();
    }

    public async Task SaveTemplate(string templateName, bool isPublic, RadzenPivotDataGrid<T> pivot = null)
    {
        EnsureInitialized();

        await using var db = await _db.CreateDbContextAsync();
        string json;

        if (IsPivotTable)
        {
            if (pivot is null)
            {
                LoadedTemplate = null;
                return;
            }

            // Auto-build PivotSettings
            PivotSettings = new PivotGridSettings
            {
                RowFields = pivot.RowsCollection.Select(x => x.Property).ToList(),
                ColumnFields = pivot.ColumnsCollection.Select(x => x.Property).ToList(),
                Aggregates = pivot.AggregatesCollection.Select(a => new PivotAggregateSetting
                {
                    Property = a.Property,
                    Title = a.Title,
                    FormatString = a.FormatString,
                    Aggregate = a.Aggregate.ToString()
                }).ToList()
            };

            json = JsonSerializer.Serialize(PivotSettings);
        }
        else
        {
            json = JsonSerializer.Serialize(Settings);
        }

        LoadedTemplate = await db.UpsertDataGridTemplate(templateName, PageName, UserName, json, isPublic);
    }

    public void SetSettingsFromTemplate(DataGridTemplate tmpl)
    {
        LoadedTemplate = tmpl;

        var json = tmpl?.DataGridSettings;
        if (string.IsNullOrWhiteSpace(json))
        {
            PivotSettings = null;
            Settings = null;
            return;
        }

        if (IsPivotTable)
            PivotSettings = JsonSerializer.Deserialize<PivotGridSettings>(json);
        else
            Settings = JsonSerializer.Deserialize<DataGridSettings>(json);
    }

    // -------------------------
    // Client-side mode
    // -------------------------
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

        // Apply dynamic filter expression
        if (!string.IsNullOrEmpty(args.Filter) &&
            !args.Filter.Replace(" ", "").Equals("0=1", StringComparison.OrdinalIgnoreCase))
        {
            query = query.AsQueryable().Where(args.Filter);
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

            if (!string.IsNullOrEmpty(args.Filter) && !IgnoreFilter)
                q = q.Where(args.Filter);

            return q;
        };

        ShouldCount = null;
    }

    public void SetData(IEnumerable<T> items, bool requestReload = true)
    {
        EnsureInitialized();

        _cache = items?.ToList() ?? new List<T>();

        // invalidate derived state (filters/search/count depend on dataset)
        ShouldCount = true;
        _lastFilter = null;
        _lastSearchText = null;
        LastBuilder = null;

        // optional: reflect immediately (useful if caller wants instant render even before grid reload)
        Data = _cache;
        Count = _cache.Count;

        if (requestReload)
            Reload();
    }
}
