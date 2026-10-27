using Npgsql;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Owns the connections the log tables are written and maintained through: a small pool of its
/// own, so logging never takes a connection the requests need and a slow or unreachable database
/// fails it fast. The data source has no logger factory on purpose: a failure of the logging
/// connections must never be logged through them again.
/// </summary>
public sealed class LogDatabase : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Identifies the name these connections report to PostgreSQL.
    /// </summary>
    public const string ApplicationName = "codigoactivo-logs";

    private const int MaxPoolSize = 3;
    private const int ConnectTimeoutSeconds = 5;
    private const int CommandTimeoutSeconds = 15;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogDatabase"/> class.
    /// </summary>
    /// <param name="connectionString">
    /// Connection string of the application database; its application name, pool size and
    /// timeouts are replaced by those of the logging pool.
    /// </param>
    public LogDatabase(string connectionString)
    {
        dataSource = NpgsqlDataSource.Create(
            new NpgsqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = ApplicationName,
                MaxPoolSize = MaxPoolSize,
                Timeout = ConnectTimeoutSeconds,
                CommandTimeout = CommandTimeoutSeconds,
            }
        );
    }

    /// <summary>
    /// Opens a connection from the logging pool.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the open connection.</returns>
    public ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        return dataSource.OpenConnectionAsync(ct);
    }

    /// <summary>
    /// Closes the pool.
    /// </summary>
    public void Dispose()
    {
        dataSource.Dispose();
    }

    /// <summary>
    /// Closes the pool.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public ValueTask DisposeAsync()
    {
        return dataSource.DisposeAsync();
    }
}
