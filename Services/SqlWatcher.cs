using AutoCAC.Extensions;
using AutoCAC.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

public sealed class SqlWatcher : IDisposable
{
    private readonly SqlConnection _connection;
    private readonly string _query;
    private readonly Func<SqlParameter[]> _parametersFactory;

    private SqlCommand _command;
    private SqlDependency _dependency;
    private SqlDataReader _reader; // 🔑 THIS IS THE FIX
    private bool _disposed;

    public event Action Changed;

    public SqlWatcher(
        IDbContextFactory<mainContext> factory,
        string query,
        Func<SqlParameter[]> parametersFactory)
    {
        _query = query;
        _parametersFactory = parametersFactory;

        var ctx = factory.CreateDbContext();
        _connection = (SqlConnection)ctx.Database.GetDbConnection();
        _connection.Open();

        Subscribe();
    }

    private void Subscribe()
    {
        if (_disposed) return;

        _command = new SqlCommand(_query, _connection);

        var parameters = _parametersFactory?.Invoke();
        if (parameters != null)
            _command.Parameters.AddRange(parameters);

        _dependency = new SqlDependency(_command);
        _dependency.OnChange += OnChange;

        _reader = _command.ExecuteReader(); // 🔑 MUST STAY ALIVE
    }

    private void OnChange(object sender, SqlNotificationEventArgs e)
    {
        _dependency.OnChange -= OnChange;

        _reader?.Dispose();
        _command?.Dispose();

        if (e.Type == SqlNotificationType.Change)
            Changed?.Invoke();

        Subscribe();
    }

    public void Dispose()
    {
        _disposed = true;

        _dependency?.OnChange -= OnChange;
        _reader?.Dispose();
        _command?.Dispose();
        _connection.Dispose();
    }
}

