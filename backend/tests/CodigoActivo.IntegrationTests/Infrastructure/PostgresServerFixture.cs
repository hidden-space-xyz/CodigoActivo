using System.Diagnostics.CodeAnalysis;
using CodigoActivo.Infrastructure.Database.Context;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(
    typeof(CodigoActivo.IntegrationTests.Infrastructure.PostgresServerFixture)
)]

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Starts one PostgreSQL server for the whole run, or uses the database that
/// <c>CODIGOACTIVO_TEST_DB_CONNECTION</c> names, and migrates that database once. It is the
/// template every test class copies into a database of its own, so the classes run in parallel
/// without seeing each other's rows.
/// </summary>
public sealed class PostgresServerFixture : IAsyncLifetime
{
    private const string ExternalConnectionEnvVar = "CODIGOACTIVO_TEST_DB_CONNECTION";
    private const string MaintenanceDatabase = "postgres";
    private const int MaxConnections = 300;

    private readonly string? externalConnectionString = Environment.GetEnvironmentVariable(
        ExternalConnectionEnvVar
    );

    private readonly PostgreSqlContainer? container;

    private NpgsqlConnectionStringBuilder template = new();

    public PostgresServerFixture()
    {
        if (string.IsNullOrWhiteSpace(externalConnectionString))
        {
            container = new PostgreSqlBuilder("postgres:18-alpine")
                .WithDatabase("codigoactivo")
                .WithUsername("codigoactivo")
                .WithPassword("codigoactivo")
                .WithCommand("-c", $"max_connections={MaxConnections}")
                .WithCleanUp(true)
                .Build();
        }
    }

    public async ValueTask InitializeAsync()
    {
        var rawConnectionString = await StartServerAsync();

        template = new NpgsqlConnectionStringBuilder(rawConnectionString)
        {
            IncludeErrorDetail = true,
        };

        await MigrateTemplateAsync();
    }

    /// <summary>
    /// Copies the migrated template into a new database and returns its connection string.
    /// </summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var database = $"test_{Guid.NewGuid():N}";
        await ExecuteOnServerAsync(
            $"CREATE DATABASE {Quote(database)} TEMPLATE {Quote(template.Database!)}"
        );

        return new NpgsqlConnectionStringBuilder(template.ConnectionString)
        {
            Database = database,
        }.ConnectionString;
    }

    /// <summary>
    /// Drops a database created by <see cref="CreateDatabaseAsync"/>, closing the connections
    /// still pooled for it.
    /// </summary>
    public async Task DropDatabaseAsync(string connectionString)
    {
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(connection);
        }

        var database = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS {Quote(database)} WITH (FORCE)");
    }

    private async Task<string> StartServerAsync()
    {
        if (container is null)
        {
            return externalConnectionString!;
        }

        try
        {
            await container.StartAsync(TestCancellation.Ct);
        }
        catch (DockerUnavailableException ex)
        {
            throw CreateContainerStartException(ex);
        }
        catch (DockerApiException ex)
        {
            throw CreateContainerStartException(ex);
        }
        catch (ContainerNotRunningException ex)
        {
            throw CreateContainerStartException(ex);
        }
        catch (HttpRequestException ex)
        {
            throw CreateContainerStartException(ex);
        }
        catch (TimeoutException ex)
        {
            throw CreateContainerStartException(ex);
        }
        catch (InvalidOperationException ex)
        {
            throw CreateContainerStartException(ex);
        }

        return container.GetConnectionString();
    }

    private static InvalidOperationException CreateContainerStartException(Exception inner)
    {
        return new InvalidOperationException(
            "Could not start the PostgreSQL test container. The integration tests need a running "
                + "Docker daemon (Docker Desktop with the Linux engine). Start Docker and retry, or "
                + $"set {ExternalConnectionEnvVar} to an Npgsql connection string pointing at an "
                + "empty, disposable PostgreSQL database whose role may create databases: it becomes "
                + "the template each test class copies instead of spawning a container.",
            inner
        );
    }

    /// <summary>
    /// Migrates the template without pooling, because PostgreSQL refuses to copy a database that
    /// still has an open connection.
    /// </summary>
    private async Task MigrateTemplateAsync()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoDbContext>();
        options.UseWriteDatabase(
            new NpgsqlConnectionStringBuilder(template.ConnectionString)
            {
                Pooling = false,
            }.ConnectionString
        );
        await using var db = new CodigoActivoDbContext(options.Options);
        await db.Database.MigrateAsync(TestCancellation.Ct);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The statements only name databases this fixture generates or reads from the test connection string; PostgreSQL takes no parameters for identifiers."
    )]
    private async Task ExecuteOnServerAsync(string sql)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(template.ConnectionString)
        {
            Database = MaintenanceDatabase,
        };
        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync(TestCancellation.Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestCancellation.Ct);
    }

    private static string Quote(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public ValueTask DisposeAsync()
    {
        return container is not null ? container.DisposeAsync() : ValueTask.CompletedTask;
    }
}
