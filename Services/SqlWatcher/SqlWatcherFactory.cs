public sealed class SqlWatcherFactory
{
    private readonly string _connectionString;

    public SqlWatcherFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqlWatcher Create(string query, Func<Task> onChangedAsync)
    {
        return new SqlWatcher(_connectionString, query, onChangedAsync);
    }
}