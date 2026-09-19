using System.Diagnostics;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Security;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class CodigoActivoWebAppFactory(PostgresContainerFixture postgres)
    : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset ClockOrigin = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string fileStorageRoot = CreateFileStorageRoot();
    private readonly string deploymentModeFile = CreateDeploymentModeFilePath();
    private readonly List<WebApplicationFactory<Program>> derived = [];

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

    private static ApiRateLimitOptions UnboundedRateLimits()
    {
        const int Unbounded = 1_000_000;
        return new ApiRateLimitOptions
        {
            AuthenticatedRequestsPerMinute = Unbounded,
            AnonymousRequestsPerMinutePerIp = Unbounded,
            CredentialRequestsPerMinutePerIp = Unbounded,
            ReportRequestsPerMinutePerUser = Unbounded,
            SingleRecipientEmailRequestsPerMinutePerUser = Unbounded,
            BulkEmailRequestsPerMinutePerUser = Unbounded,
            FileUploadRequestsPerMinutePerUser = Unbounded,
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

    public WebApplicationFactory<Program> WithRateLimits(ApiRateLimitOptions limits)
    {
        return Track(
            WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<ApiRateLimitOptions>();
                    services.AddSingleton(limits);
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

        builder.UseSetting("DEMO_MODE", "false");
        builder.UseSetting("DOTNET_RUNNING_IN_CONTAINER", "true");
        builder.UseSetting("BOOTSTRAP_ADMIN_EMAIL", "bootstrap@codigoactivo.test");
        builder.UseSetting("BOOTSTRAP_ADMIN_PASSWORD", "bootstrap-password-123");
        builder.UseSetting("SMTP_HOST", "smtp.test");
        builder.UseSetting("SMTP_FROM_ADDRESS", "no-reply@codigoactivo.test");

        builder.ConfigureTestServices(services =>
        {
            RemoveHostedService<ExpiredSessionCleaner>(services);
            RemoveHostedService<EmailOutboxProcessor>(services);

            services.RemoveAll<DeploymentModeLock>();
            services.AddSingleton(sp => new DeploymentModeLock(
                deploymentModeFile,
                sp.GetRequiredService<ILogger<DeploymentModeLock>>()
            ));

            UseTestDatabase(services);

            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher, FakePasswordHasher>();

            services.RemoveAll<ISecretProtector>();
            services.AddSingleton<ISecretProtector, FakeSecretProtector>();

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            services.RemoveAll<IEmailTransport>();
            services.AddSingleton<IEmailTransport>(EmailSender);

            UseSynchronousEmailOutbox(services);

            services.RemoveAll<EmailGuardOptions>();
            services.AddSingleton(UnboundedEmailGuard());

            services.RemoveAll<ApiRateLimitOptions>();
            services.AddSingleton(UnboundedRateLimits());

            services.RemoveAll<FileStorageOptions>();
            services.AddSingleton(new FileStorageOptions { RootPath = fileStorageRoot });
        });
    }

    /// <summary>
    /// Drops a background worker whose timing would race the tests: the periodic session purge would
    /// delete rows behind a test that moves the clock past a session expiry on purpose, and the email
    /// delivery worker would compete with the synchronous drain installed by
    /// <see cref="UseSynchronousEmailOutbox"/>. Both are exercised directly instead.
    /// </summary>
    /// <typeparam name="T">Hosted service to unregister.</typeparam>
    private static void RemoveHostedService<T>(IServiceCollection services)
        where T : IHostedService
    {
        var descriptors = services
            .Where(descriptor => descriptor.ImplementationType == typeof(T))
            .ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    /// <summary>
    /// Keeps the real PostgreSQL outbox but delivers it inline: a request that queues email leaves
    /// with the message already handed to <see cref="FakeEmailSender"/>, so tests observe mail
    /// without polling, sleeping or racing a background worker.
    /// </summary>
    private static void UseSynchronousEmailOutbox(IServiceCollection services)
    {
        services.RemoveAll<IEmailOutbox>();
        services.AddSingleton<IEmailOutbox, DrainingEmailOutbox>();
    }

    /// <summary>
    /// Delivers whatever the outbox holds and is due, for tests that queue email indirectly or move
    /// the clock to a scheduled retry.
    /// </summary>
    public Task DrainEmailOutboxAsync()
    {
        return Services.GetRequiredService<IEmailOutbox>() is DrainingEmailOutbox draining
            ? draining.DrainAsync(TestCancellation.Ct)
            : Task.CompletedTask;
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
        var deploymentStateRoot = Path.GetDirectoryName(deploymentModeFile);
        if (deploymentStateRoot is not null)
        {
            TryDeleteDirectory(deploymentStateRoot);
        }
    }

    private static string CreateFileStorageRoot()
    {
        var root = Path.Join(
            Path.GetTempPath(),
            "codigoactivo-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        return root;
    }

    private static string CreateDeploymentModeFilePath()
    {
        return Path.Join(
            Path.GetTempPath(),
            "codigoactivo-tests",
            Guid.NewGuid().ToString("N"),
            "deployment-mode"
        );
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
