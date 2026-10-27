using System.Diagnostics;
using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.IntegrationTests.Diagnostics;

public sealed class LoggingIsolationTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    private WebApplicationFactory<Program> WithUnreachableLogDatabase()
    {
        return Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<LogDatabase>();
                services.AddSingleton(_ => new LogDatabase(
                    "Host=127.0.0.1;Port=1;Database=unused;Username=unused"
                ));
                services.TryAddEnumerable(
                    ServiceDescriptor.Singleton<ILoggerProvider, DatabaseLoggerProvider>()
                );
                services.RemoveAll<IAuditTrail>();
                services.AddSingleton<IAuditTrail>(provider => new SerilogAuditTrail(
                    provider.GetRequiredService<LogDatabase>(),
                    provider.GetRequiredService<LogPartitionMaintainer>(),
                    provider.GetRequiredService<IClock>()
                ));
            })
        );
    }

    [Fact]
    public async Task RequestsKeepAnsweringWhileTheLogDatabaseIsUnreachable()
    {
        await using var host = WithUnreachableLogDatabase();
        var logger = host
            .Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Tests.Isolation");
        var elapsed = Stopwatch.StartNew();

        for (var i = 0; i < 15_000; i++)
        {
            logger.EmailDailyLimitReached(i);
        }

        var client = await LoginAsync(host, TestSeedData.MemberCredentials);
        foreach (var url in new[] { "/api/auth/me", "/api/news", "/api/events" })
        {
            using var response = await client.GetAsync(TestUri.Rel(url), Ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK, url);
        }

        using var logout = await client.PostJsonAsync("/api/auth/logout", body: null, Ct);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        elapsed
            .Elapsed.Should()
            .BeLessThan(Budget, "logging only enqueues and never waits for its database");
    }
}
