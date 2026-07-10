using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Security;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class CodigoActivoWebAppFactory(PostgresContainerFixture postgres)
    : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset ClockOrigin = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string fileStorageRoot = CreateFileStorageRoot();

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

            services.RemoveAll<FileStorageOptions>();
            services.AddSingleton(
                new FileStorageOptions
                {
                    RootPath = fileStorageRoot,
                    MaxSizeBytes = FileStorageOptions.DefaultMaxSizeBytes,
                }
            );
        });
    }

    private void UseTestDatabase(IServiceCollection services)
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

        services.AddDbContext<CodigoActivoDbContext>(options =>
            options
                .UseNpgsql(
                    postgres.ConnectionString,
                    npgsql =>
                        npgsql.MigrationsAssembly(typeof(CodigoActivoDbContext).Assembly.FullName)
                )
                .UseSnakeCaseNamingConvention()
        );
    }

    public async Task ResetDatabaseAsync()
    {
        EmailSender.Clear();
        ResetClock();

        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<CodigoActivoDbContext>();

        await TestDatabase.TruncateAllTablesAsync(db);

        await provider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        await TestSeedData.SeedUsersAsync(db);
    }

    private void ResetClock()
    {
        Clock.UtcNow = ClockOrigin;
        Clock.Today = new DateOnly(2026, 7, 4);
        Clock.TimeZone = TimeZoneInfo.Utc;
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

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        TryDeleteDirectory(fileStorageRoot);
    }

    private static string CreateFileStorageRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "codigoactivo-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
