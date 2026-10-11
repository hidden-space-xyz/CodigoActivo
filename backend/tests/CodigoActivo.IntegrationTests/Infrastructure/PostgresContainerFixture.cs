using CodigoActivo.Infrastructure.Database.Context;
using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(
    typeof(CodigoActivo.IntegrationTests.Infrastructure.PostgresContainerFixture)
)]

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string ExternalConnectionEnvVar = "CODIGOACTIVO_TEST_DB_CONNECTION";

    private readonly string? externalConnectionString = Environment.GetEnvironmentVariable(
        ExternalConnectionEnvVar
    );

    private readonly PostgreSqlContainer? container;

    public PostgresContainerFixture()
    {
        if (string.IsNullOrWhiteSpace(externalConnectionString))
        {
            container = new PostgreSqlBuilder("postgres:18-alpine")
                .WithDatabase("codigoactivo")
                .WithUsername("codigoactivo")
                .WithPassword("codigoactivo")
                .WithCleanUp(true)
                .Build();
        }
    }

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var rawConnectionString = await StartServerAsync();

        ConnectionString = new NpgsqlConnectionStringBuilder(rawConnectionString)
        {
            IncludeErrorDetail = true,
        }.ConnectionString;

        await MigrateSchemaOnceAsync();
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
                + "empty, disposable PostgreSQL database to reuse instead of spawning a container.",
            inner
        );
    }

    public CodigoActivoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoDbContext>();
        options.UseWriteDatabase(ConnectionString);
        return new CodigoActivoDbContext(options.Options);
    }

    public CodigoActivoReadDbContext CreateReadContext()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoReadDbContext>();
        options.UseReadDatabase(ConnectionString);
        return new CodigoActivoReadDbContext(options.Options);
    }

    private async Task MigrateSchemaOnceAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync(TestCancellation.Ct);
    }

    public ValueTask DisposeAsync()
    {
        return container is not null ? container.DisposeAsync() : ValueTask.CompletedTask;
    }
}
