using AutoCAC.Extensions;
using AutoCAC.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

public class SqlWatcher : IDisposable
{
    private readonly IDbContextFactory<mainContext> _factory;
    private readonly string _query;
    private readonly SqlParameter[] _parameters;
    private readonly Action _onChange;

    private readonly SqlConnection _connection;
    private readonly SqlCommand _command;
    private SqlDependency _dependency;
    private bool _disposed = false;

    private SqlWatcher(
        IDbContextFactory<mainContext> factory,
        string query,
        SqlParameter[] parameters,
        Action onChange)
    {
        _factory = factory;
        _query = query;
        _parameters = parameters;
        _onChange = onChange;

        var context = _factory.CreateDbContext();
        _connection = (SqlConnection)context.Database.GetDbConnection();
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        _command = new SqlCommand(_query, _connection);
        if (_parameters != null)
            _command.Parameters.AddRange(_parameters);

        Subscribe();
    }

    private void Subscribe()
    {
        _dependency = new SqlDependency(_command);
        _dependency.OnChange += HandleChange;
        _command.ExecuteReader();
    }

    private void HandleChange(object sender, SqlNotificationEventArgs e)
    {
        if (e.Type == SqlNotificationType.Change)
            _onChange?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _dependency.OnChange -= HandleChange;
        _command?.Dispose();
        _connection?.Dispose();
        _disposed = true;
    }

    public SqlWatcher Resubscribe()
    {
        Dispose();
        return new SqlWatcher(_factory, _query, _parameters, _onChange);
    }

    public static SqlWatcher CreateDataImportStatus(
        IDbContextFactory<mainContext> factory,
        int jobId,
        string tableName,
        Action onChange,
        bool setToRequested = false
        )
    {
        if (setToRequested)
        {
            _ = factory.ExecuteSqlAsync($@"
                MERGE INTO dbo.DataImportStatus AS target
                USING (SELECT {jobId} AS JobId, {tableName} AS TableName) AS source
                ON target.JobId = source.JobId AND target.TableName = source.TableName
                WHEN MATCHED THEN
                    UPDATE SET Status = 'REQUESTED'
                WHEN NOT MATCHED THEN
                    INSERT (JobId, TableName, Status) VALUES ({jobId}, {tableName}, 'REQUESTED');");
        }

        var query = @"SELECT Status FROM dbo.DataImportStatus 
                      WHERE JobId = @jobId AND TableName = @tableName";

        var parameters = new[]
        {
            new SqlParameter("@jobId", jobId),
            new SqlParameter("@tableName", tableName)
        };

        return new SqlWatcher(factory, query, parameters, onChange);
    }
}

