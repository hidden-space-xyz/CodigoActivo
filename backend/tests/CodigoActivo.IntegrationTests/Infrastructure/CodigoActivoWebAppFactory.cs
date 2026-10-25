using System.Diagnostics;
using System.Security.Cryptography;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Composition.Platform;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.Infrastructure.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class CodigoActivoWebAppFactory : WebApplicationFactory<Program>
{
    private static readonly DateTimeOffset ClockOrigin = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresContainerFixture postgres;
    private readonly string fileStorageRoot = CreateFileStorageRoot();
    private readonly string deploymentModeFile = CreateDeploymentModeFilePath();
    private readonly string keyRingDirectory = CreateKeyRingDirectoryPath();
    private readonly string dataProtectionKey = Convert.ToBase64String(
        RandomNumberGenerator.GetBytes(DataProtectionRootKey.Size)
    );
    private readonly List<WebApplicationFactory<Program>> derived = [];

    /// <summary>
    /// Sends every request over HTTPS, as the production proxy does, so the HTTPS redirection
    /// lets it through instead of warning that it has no port to redirect to.
    /// </summary>
    public CodigoActivoWebAppFactory(PostgresContainerFixture postgres)
    {
        this.postgres = postgres;
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public TestClock Clock { get; } = new();

    public FakeEmailSender EmailSender { get; } = new();

    public LogRecorder Logs { get; } = new();

    public RecordingAuditTrail Audit { get; } = new();

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
        builder.UseSetting("DATA_PROTECTION_KEY", dataProtectionKey);
        builder.UseSetting("SMTP_HOST", "smtp.test");
        builder.UseSetting("SMTP_FROM_ADDRESS", "no-reply@codigoactivo.test");
        builder.UseSetting("LEGAL_NAME", "Asociación Código Activo");
        builder.UseSetting("LEGAL_TAX_ID", "G24000000");
        builder.UseSetting("LEGAL_ADDRESS", "Calle Ejemplo 1, 24001 León");
        builder.UseSetting(
            "LEGAL_REGISTRY",
            "Registro de Asociaciones de Castilla y León, n.º 0000"
        );
        builder.UseSetting("LEGAL_EMAIL", "contacto@codigoactivo.test");
        builder.UseSetting("PRIVACY_OFFICER_EMAIL", TestSeedData.AdminEmail);
        builder.UseSetting("LEGAL_PHONE", "987 00 00 00");

        builder.ConfigureTestServices(services =>
        {
            RemoveHostedService<ExpiredSessionCleanupWorker>(services);
            RemoveHostedService<DataHistoryPurgeWorker>(services);
            RemoveHostedService<UnverifiedAccountPurgeWorker>(services);
            RemoveHostedService<AdultDependentPurgeWorker>(services);
            RemoveHostedService<UnusedFilePurgeWorker>(services);
            RemoveHostedService<EmailOutboxDeliveryWorker>(services);
            RemoveHostedService<DisposableEmailDomainRefreshWorker>(services);
            RemoveHostedService<LogRetentionWorker>(services);

            services.RemoveAll<DeploymentModeLock>();
            services.AddSingleton(new DeploymentModeLock(deploymentModeFile));

            UseRecordedLogs(services);

            UseTestDatabase(services);

            services.RemoveAll<LogDatabase>();
            services.AddSingleton(_ => new LogDatabase(postgres.ConnectionString));
            services.RemoveAll<IAuditTrail>();
            services.AddSingleton<IAuditTrail>(Audit);

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

            services.RemoveAll<KeyRingOptions>();
            services.AddSingleton(new KeyRingOptions { DirectoryPath = keyRingDirectory });
        });
    }

    /// <summary>
    /// Drops a background worker whose timing would race the tests: the periodic session and deleted
    /// account purges would delete rows behind a test that moves the clock on purpose, the email
    /// delivery worker would compete with the synchronous drain installed by
    /// <see cref="UseSynchronousEmailOutbox"/>, the disposable email domain refresh would reach
    /// the internet and fill the list behind the tests, and the log retention would drop the
    /// partitions a test prepared. All are exercised directly instead.
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
    /// Replaces the database provider with <see cref="LogRecorder"/>: the tests read what the
    /// level policy lets through without waiting for the batching sink, and the log tables only
    /// hold what a test writes into them on purpose.
    /// </summary>
    private void UseRecordedLogs(IServiceCollection services)
    {
        var database = services
            .Where(descriptor => descriptor.ImplementationType == typeof(DatabaseLoggerProvider))
            .ToList();
        foreach (var descriptor in database)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<ILoggerProvider>(Logs);
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
                || d.ServiceType == typeof(CodigoActivoReadDbContext)
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
            options.UseWriteDatabase(postgres.ConnectionString)
        );
        services.AddDbContext<CodigoActivoReadDbContext>(options =>
            options.UseReadDatabase(postgres.ConnectionString)
        );
    }

    public async Task ResetDatabaseAsync()
    {
        EmailSender.Clear();
        Logs.Clear();
        Audit.Clear();
        ResetClock();
        await SeedFreshDatabaseAsync();
        await ResetCachesAsync();
    }

    public async Task EmptyDatabaseAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
    }

    private async Task SeedFreshDatabaseAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
        await new DatabaseSeeder(db).SeedAsync(TestCancellation.Ct);
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
        TryDeleteDirectory(keyRingDirectory);
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

    private static string CreateKeyRingDirectoryPath()
    {
        return Path.Join(Path.GetTempPath(), "codigoactivo-tests", Guid.NewGuid().ToString("N"));
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
