using CodigoActivo.Infrastructure.Database.Context;
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
            container = new PostgreSqlBuilder("postgres:17-alpine")
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
            return externalConnectionString!;

        try
        {
            await container.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start the PostgreSQL test container. The integration tests need a running "
                    + "Docker daemon (Docker Desktop with the Linux engine). Start Docker and retry, or "
                    + $"set {ExternalConnectionEnvVar} to an Npgsql connection string pointing at an "
                    + "empty, disposable PostgreSQL database to reuse instead of spawning a container.",
                ex
            );
        }

        return container.GetConnectionString();
    }

    private async Task MigrateSchemaOnceAsync()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoDbContext>()
            .UseNpgsql(
                ConnectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(CodigoActivoDbContext).Assembly.FullName)
            )
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = new CodigoActivoDbContext(options);
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync()
    {
        return container is not null ? container.DisposeAsync() : ValueTask.CompletedTask;
    }
}
