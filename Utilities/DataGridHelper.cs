// Utilities/DataGridHelper.cs
using AutoCAC.Extensions;
using AutoCAC.Models;
using AutoCAC.Utilities;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
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
    public IEnumerable<T> Data { get; private set; }
    public int Count { get; private set; }

    // grid UI state
    public DataGridSettings Settings { get; set; }
    private string _dataGridName;
    public string PageName =>
        !string.IsNullOrWhiteSpace(_dataGridName)
            ? _dataGridName!
            : typeof(T).Name;

    public string LoadedTemplateName {  get; set; }

    public DataGridHelper(
        IDbContextFactory<AutoCAC.Models.mainContext> db,
        Func<AutoCAC.Models.mainContext, IQueryable<T>> source = null,
        bool ignoreFilter = false,
        string[] searchColumns = null,
        string dataGridName = null)
    {
        _db = db;
        Source = source ?? DefaultQuery;
        IgnoreFilter = ignoreFilter;
        SearchColumns = searchColumns;
        Data = null;
        ShouldCount = true;
        _dataGridName = dataGridName;
    }

    public void SetQuickFilter(string text)
    {
        SearchText = text ?? string.Empty;
        ShouldCount = true;
    }

    public async Task LoadAsync(LoadDataArgs args, CancellationToken ct = default)
    {
        await using var ctx = await _db.CreateDbContextAsync(ct);
        IQueryable<T> query = Source(ctx).AsNoTracking();

        if (!string.IsNullOrEmpty(args.Filter) && !IgnoreFilter)
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

        var visibleProps = Settings != null
            ? Settings.Columns
                .Where(c => c.Visible)
                .Select(c => c.Property)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .ToList()
            : null;

        await query.DownloadAsCsvAsync(new LoadDataArgs(), js, includeProperties: visibleProps);
    }

    public async Task<List<DataGridTemplate>> GetTemplatesAsync(string username)
    {
        await using var db = await _db.CreateDbContextAsync();
        return await db.DataGridTemplates
                    .AsNoTracking()
                    .Where(t => t.DataGridName == PageName && t.CreatedBy == username)
                    .ToListAsync();
    }

    public async Task SaveTemplate(string templateName, string username, bool isPublic)
    {
        await using var db = await _db.CreateDbContextAsync();
        await db.UpsertDataGridTemplate(templateName, PageName, username, Settings, isPublic);
        LoadedTemplateName = templateName;
    }

    public void SetSettingsFromTemplate(DataGridTemplate tmpl)
    {
        LoadedTemplateName = string.IsNullOrWhiteSpace(tmpl?.TemplateName) ? "" : tmpl.TemplateName;
        Settings = string.IsNullOrWhiteSpace(tmpl?.DataGridSettings)
            ? null
            : JsonSerializer.Deserialize<DataGridSettings>(tmpl.DataGridSettings);
    }
}
