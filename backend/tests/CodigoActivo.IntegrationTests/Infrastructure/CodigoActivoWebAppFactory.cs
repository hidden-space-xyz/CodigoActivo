using System.Globalization;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Security;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class CodigoActivoWebAppFactory : WebApplicationFactory<Program>
{
    public TestClock Clock { get; } = new();

    public FakeEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        Environment.SetEnvironmentVariable("SMTP_HOST", "smtp.test");
        Environment.SetEnvironmentVariable("SMTP_FROM_ADDRESS", "no-reply@codigoactivo.test");

        builder.ConfigureAppConfiguration(
            (_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["AUTH_SAMESITE"] = "Lax",
                        ["DEMO_MODE"] = "false",
                    }
                );
            }
        );

        builder.ConfigureTestServices(services =>
        {
            UseTestDatabase(services);

            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher, FakePasswordHasher>();

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.RemoveAll<AccountVerificationOptions>();
            services.AddSingleton(new AccountVerificationOptions { Required = true });
        });
    }

    private static void UseTestDatabase(IServiceCollection services)
    {
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(CodigoActivoDbContext)
                || (
                    d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal)
                    ?? false
                )
            )
            .ToList();
        foreach (var descriptor in toRemove)
            services.Remove(descriptor);

        services.AddDbContext<CodigoActivoDbContext>(
            (sp, options) =>
                options
                    .UseNpgsql(
                        BuildTestConnectionString(sp.GetRequiredService<IConfiguration>()),
                        npgsql =>
                            npgsql.MigrationsAssembly(
                                typeof(CodigoActivoDbContext).Assembly.FullName
                            )
                    )
                    .UseSnakeCaseNamingConvention()
        );
    }

    private static string BuildTestConnectionString(IConfiguration configuration)
    {
        var password = configuration["POSTGRES_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "POSTGRES_PASSWORD (and optionally POSTGRES_HOST/POSTGRES_PORT/POSTGRES_DB/"
                    + "POSTGRES_USER) must be set as environment variables to run the integration tests."
            );
        }

        var database = configuration["POSTGRES_DB"] ?? "codigoactivo";
        return new NpgsqlConnectionStringBuilder
        {
            Host = configuration["POSTGRES_HOST"] ?? "localhost",
            Port = int.TryParse(
                configuration["POSTGRES_PORT"],
                CultureInfo.InvariantCulture,
                out var port
            )
                ? port
                : 5432,
            Database = $"{database}test",
            Username = configuration["POSTGRES_USER"] ?? "codigoactivo",
            Password = password,
        }.ConnectionString;
    }

    public async Task ResetDatabaseAsync()
    {
        EmailSender.Clear();

        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<CodigoActivoDbContext>();

        await TruncateAllTablesAsync(db);

        await provider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        await TestSeedData.SeedUsersAsync(db);
    }

    public async Task SeedAsync(Func<CodigoActivoDbContext, Task> seed)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<CodigoActivoDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        return await query(db);
    }

    private static async Task TruncateAllTablesAsync(CodigoActivoDbContext db)
    {
        var tables = db.Model.GetEntityTypes()
            .Select(entity => (Schema: entity.GetSchema(), Table: entity.GetTableName()))
            .Where(entity => entity.Table is not null)
            .Select(entity =>
                entity.Schema is null
                    ? $"\"{entity.Table}\""
                    : $"\"{entity.Schema}\".\"{entity.Table}\""
            )
            .Distinct()
            .ToList();

        if (tables.Count == 0)
            return;

        var sql = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
