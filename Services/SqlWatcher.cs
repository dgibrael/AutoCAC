using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading;

public sealed class SqlWatcher : IDisposable
{
    private readonly string _connectionString;
    private readonly string _query;
    private readonly Func<SqlParameter[]> _parametersFactory;
    private readonly TimeSpan _debounceWindow;

    private SqlConnection _connection;
    private SqlCommand _command;
    private SqlDependency _dependency;

    private int _raiseScheduled;
    private bool _disposed;

    public event Func<Task> ChangedAsync;

    public SqlWatcher(string connectionString, string query, Func<SqlParameter[]> parametersFactory, TimeSpan? debounceWindow = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _parametersFactory = parametersFactory;
        _debounceWindow = debounceWindow ?? TimeSpan.FromMilliseconds(500);

        Subscribe();
    }

    private void Subscribe()
    {
        if (_disposed) return;

        Cleanup();

        _connection = new SqlConnection(_connectionString);
        _connection.Open();

        _command = new SqlCommand(_query, _connection) { CommandType = CommandType.Text };

        var parameters = _parametersFactory?.Invoke();
        if (parameters != null && parameters.Length > 0)
            _command.Parameters.AddRange(parameters);

        _dependency = new SqlDependency(_command);
        _dependency.OnChange += OnChange;

        using var reader = _command.ExecuteReader();
    }

    private void OnChange(object sender, SqlNotificationEventArgs e)
    {
        if (_disposed) return;

        _dependency?.OnChange -= OnChange;

        // resubscribe immediately
        Subscribe();
        if (e.Type == SqlNotificationType.Change)
            ScheduleRaise();
    }

    private void ScheduleRaise()
    {
        if (_disposed) return;

        if (_debounceWindow <= TimeSpan.Zero)
        {
            _ = FireAsync();
            return;
        }

        if (Interlocked.Exchange(ref _raiseScheduled, 1) == 1)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_debounceWindow).ConfigureAwait(false);
                if (!_disposed)
                    await FireAsync().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref _raiseScheduled, 0);
            }
        });
    }

    private async Task FireAsync()
    {
        var handlers = ChangedAsync;
        if (handlers == null) return;

        // Let multicast invocation handle multiple subscribers:
        // it returns the Task of the *last* subscriber, so we manually await all.
        var tasks = handlers.GetInvocationList()
            .Cast<Func<Task>>()
            .Select(h => SafeInvoke(h));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        static async Task SafeInvoke(Func<Task> h)
        {
            try { await h().ConfigureAwait(false); }
            catch { /* log if desired */ }
        }
    }

    private void Cleanup()
    {
        _dependency?.OnChange -= OnChange;
        _dependency = null;

        _command?.Dispose();
        _command = null;

        _connection?.Dispose();
        _connection = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
    }
}
