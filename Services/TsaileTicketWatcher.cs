namespace AutoCAC.Services;

public sealed class TsaileTicketWatcher : IDisposable
{
    private readonly SqlWatcher _watcher;

    public event Func<Task> QueueChangedAsync;

    public TsaileTicketWatcher(SqlWatcherFactory sqlWatcherFactory)
    {
        _watcher = sqlWatcherFactory.Create(
            """
            SELECT COUNT_BIG(*)
            FROM dbo.tsaile_betterq
            """,
            OnSqlChangedAsync);
    }

    private Task OnSqlChangedAsync()
    {
        // If nobody subscribed, do nothing.
        var handlers = QueueChangedAsync;
        if (handlers == null)
            return Task.CompletedTask;

        // Invoke all subscribers and return a combined Task.
        // (If you prefer sequential invocation, I can show that too.)
        var tasks = handlers.GetInvocationList()
            .Cast<Func<Task>>()
            .Select(h => h());

        return Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
