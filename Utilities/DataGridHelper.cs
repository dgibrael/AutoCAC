// Utilities/DataGridHelper.cs
using AutoCAC.Extensions;
using AutoCAC.Models;
using AutoCAC.Utilities;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;
using System.Text.Json;
namespace AutoCAC;

public sealed class DataGridHelper<T> where T : class
{
    private readonly IDbContextFactory<AutoCAC.Models.mainContext> _db;
    private static readonly Func<AutoCAC.Models.mainContext, IQueryable<T>> DefaultQuery =
        db => db.Set<T>().AsQueryable();

    private readonly ParsingConfig _config = new() { RestrictOrderByToPropertyOrField = false };
    private ColumnFilterChoices<T> _filterChoices;

    // configuration
    public Func<AutoCAC.Models.mainContext, IQueryable<T>> Source { get; }
    public bool IgnoreFilter { get; }
    public string[] SearchColumns { get; set; }

    // state
    private string _lastFilter;
    private string _lastSearchText;
    public string SearchText { get; set; }
    public bool? ShouldCount { get; set; }
    public Func<AutoCAC.Models.mainContext, IQueryable<T>> LastBuilder { get; private set; }

    // output
    public IEnumerable<T> Data { get; set; }
    public int Count { get; set; }

    // grid UI state
    public DataGridSettings Settings { get; set; }
    public PivotGridSettings PivotSettings { get; set;  }
    private string _dataGridName;
    public string PageName =>
        !string.IsNullOrWhiteSpace(_dataGridName)
            ? _dataGridName!
            : typeof(T).Name;

    public DataGridTemplate LoadedTemplate { get; set; }
    public string UserName { get; set; }
    public bool UseClientSideData { get; set; } = false;
    private List<T> _cache;
    private bool IsPivotTable { get; set; }
    public DataGridHelper(
        IDbContextFactory<AutoCAC.Models.mainContext> db,
        string username,
        Func<AutoCAC.Models.mainContext, IQueryable<T>> source = null,
        string[] searchColumns = null,
        string dataGridName = null,
        bool ignoreFilter = false,
        bool useClientSideData = false,
        string initialSearchText = "",
        bool pivotTable = false
        )
    {
        _db = db;
        UserName = username;
        Source = source ?? DefaultQuery;
        IgnoreFilter = ignoreFilter;
        SearchColumns = searchColumns;
        Data = null;
        ShouldCount = true;
        _dataGridName = dataGridName;
        UseClientSideData = useClientSideData;
        SearchText = initialSearchText;
        IsPivotTable = pivotTable;
    }

    public void SetQuickFilter(string text)
    {
        SearchText = text ?? string.Empty;
        ShouldCount = true;
    }

    public async Task LoadAsync(LoadDataArgs args, CancellationToken ct = default)
    {
        if (UseClientSideData)
        {
            await LoadClientSideAsync(args, ct);
            return;
        }
        await using var ctx = await _db.CreateDbContextAsync(ct);
        IQueryable<T> query = Source(ctx).AsNoTracking();
        if (!string.IsNullOrEmpty(args.Filter) && !args.Filter.Replace(" ", "").Equals("0=1", StringComparison.OrdinalIgnoreCase))
            query = query.Where(args.Filter);

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
            var q = Source(c).AsNoTracking();
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
        args.Top = null;
        args.Skip = null;

        await using var ctx = await _db.CreateDbContextAsync();
        var query = Source(ctx).AsNoTracking();

        _filterChoices ??= new ColumnFilterChoices<T>();
        await _filterChoices.GetColumnFilterDataAsync(args, query);
    }

    public async Task DownloadCsvAsync(IJSRuntime js, int hardLimit = 100000, CancellationToken ct = default)
    {
        if (Count > hardLimit)
            throw new InvalidOperationException("Data set too large to download. Filter first.");

        await using var ctx = await _db.CreateDbContextAsync(ct);
        var query = (LastBuilder ?? Source)(ctx);
        
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

    public async Task<List<DataGridTemplate>> GetTemplatesAsync()
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.DataGridTemplates
                    .AsNoTracking()
                    .Where(t => t.DataGridName == PageName && t.CreatedBy == UserName)
                    .ToListAsync();
    }

    public async Task SaveTemplate(string templateName, bool isPublic, RadzenPivotDataGrid<T> pivot = null)
    {
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

    private async Task LoadClientSideAsync(LoadDataArgs args, CancellationToken ct)
    {
        // Load full data set if we don't have it yet
        if (_cache == null)
        {
            await using var ctx = await _db.CreateDbContextAsync(ct);
            _cache = await Source(ctx).AsNoTracking().ToListAsync(ct);
        }

        IEnumerable<T> query = _cache;

        // Apply search
        if (!string.IsNullOrWhiteSpace(SearchText) && SearchColumns.Length > 0)
        {
            query = query.AsQueryable().QuickSearch(SearchText, SearchColumns);
        }

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

            if (!string.IsNullOrWhiteSpace(SearchText) && SearchColumns.Length > 0)
                q = q.QuickSearch(SearchText, SearchColumns);

            if (!string.IsNullOrEmpty(args.Filter) && !IgnoreFilter)
                q = q.Where(args.Filter);

            return q;
        };

        ShouldCount = null;
    }
}
