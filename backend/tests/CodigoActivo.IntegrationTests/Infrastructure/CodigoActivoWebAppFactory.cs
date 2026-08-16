using System.Diagnostics;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Security;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class CodigoActivoWebAppFactory(PostgresContainerFixture postgres)
    : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset ClockOrigin = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string fileStorageRoot = CreateFileStorageRoot();
    private readonly List<WebApplicationFactory<Program>> derived = [];

    private WebApplicationFactory<Program>? verificationDisabled;

    public TestClock Clock { get; } = new();

    public FakeEmailSender EmailSender { get; } = new();

    private static EmailGuardOptions UnboundedEmailGuard()
    {
        const int Unbounded = 1_000_000;
        return new EmailGuardOptions
        {
            RecipientBurst = Unbounded,
            RecipientPerHour = Unbounded,
            RecipientPerDay = Unbounded,
            GlobalBurst = Unbounded,
            GlobalPerHour = Unbounded,
            GlobalCredentialReserve = 0,
        };
    }

    public WebApplicationFactory<Program> WithEmailGuard(EmailGuardOptions guard)
    {
        return Track(
            WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<EmailGuardOptions>();
                    services.AddSingleton(guard);
                })
            )
        );
    }

    public WebApplicationFactory<Program> WithVerificationDisabled()
    {
        return verificationDisabled ??= Track(
            WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<AccountVerificationOptions>();
                    services.AddSingleton(new AccountVerificationOptions { Required = false });
                })
            )
        );
    }

    private WebApplicationFactory<Program> Track(WebApplicationFactory<Program> factory)
    {
        derived.Add(factory);
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("AUTH_SAMESITE", "Lax");
        builder.UseSetting("DEMO_MODE", "false");
        builder.UseSetting("SMTP_HOST", "smtp.test");
        builder.UseSetting("SMTP_FROM_ADDRESS", "no-reply@codigoactivo.test");

        builder.ConfigureTestServices(services =>
        {
            UseTestDatabase(services);

            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher, FakePasswordHasher>();

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            services.RemoveAll<IEmailTransport>();
            services.AddSingleton<IEmailTransport>(EmailSender);

            services.RemoveAll<IEmailDispatcher>();
            services.AddSingleton<IEmailDispatcher>(EmailSender);

            services.RemoveAll<EmailGuardOptions>();
            services.AddSingleton(UnboundedEmailGuard());

            services.RemoveAll<AccountVerificationOptions>();
            services.AddSingleton(new AccountVerificationOptions { Required = true });

            services.RemoveAll<FileStorageOptions>();
            services.AddSingleton(new FileStorageOptions { RootPath = fileStorageRoot });
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
        {
            services.Remove(descriptor);
        }

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
        await ResetCachesAsync();

        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<CodigoActivoDbContext>();

        await TestDatabase.TruncateAllTablesAsync(db);

        await provider.GetRequiredService<DatabaseSeeder>().SeedAsync(TestCancellation.Ct);
        await TestSeedData.SeedUsersAsync(db, TestCancellation.Ct);
    }

    private void ResetClock()
    {
        Clock.UtcNow = ClockOrigin;
        Clock.Today = new DateOnly(2026, 7, 4);
        Clock.TimeZone = TimeZoneInfo.Utc;
    }

    private async Task ResetCachesAsync()
    {
        await PurgeCachesAsync(Services);
        foreach (var factory in derived)
        {
            await PurgeCachesAsync(factory.Services);
        }
    }

    private static async Task PurgeCachesAsync(IServiceProvider services)
    {
        await services.GetRequiredService<ICacheInvalidator>().InvalidateAsync(CacheTags.All);
    }

    public async Task SeedAsync(Func<CodigoActivoDbContext, Task> seed)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        await seed(db);
        await db.SaveChangesAsync(TestCancellation.Ct);
        await ResetCachesAsync();
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
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Test file storage cleanup failed for '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Test file storage cleanup failed for '{path}': {ex.Message}");
        }
    }
}
