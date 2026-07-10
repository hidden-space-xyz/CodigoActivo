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

    private WebApplicationFactory<Program>? verificationDisabled;

    public TestClock Clock { get; } = new();

    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>
    /// A sibling host with <c>ACCOUNT_VERIFICATION_REQUIRED=false</c>, sharing this factory's
    /// database and fakes. Built once per test class (this factory is an <c>IClassFixture</c>) and
    /// torn down with it, rather than once per test.
    /// </summary>
    public WebApplicationFactory<Program> WithVerificationDisabled()
    {
        return verificationDisabled ??= WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AccountVerificationOptions>();
                services.AddSingleton(new AccountVerificationOptions { Required = false });
            })
        );
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting, not ConfigureAppConfiguration: Program reads AUTH_SAMESITE and (through
        // AddCodigoActivo) SMTP_* from builder.Configuration while the WebApplicationBuilder is still
        // being assembled, which is before ConfigureAppConfiguration sources are merged. UseSetting
        // lands in host configuration early enough, and unlike Environment.SetEnvironmentVariable it
        // does not mutate process-global state shared by every other test host.
        builder.UseSetting("AUTH_SAMESITE", "Lax");
        builder.UseSetting("DEMO_MODE", "false");
        builder.UseSetting("SMTP_HOST", "smtp.test");
        builder.UseSetting("SMTP_FROM_ADDRESS", "no-reply@codigoactivo.test");
        // Otherwise every test host rolls its own numbered log file into src/CodigoActivo.API/logs.
        builder.UseSetting("LOG_TO_FILE", "false");

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
