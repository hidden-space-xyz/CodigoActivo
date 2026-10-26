using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.API.Middlewares;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.Infrastructure.Time;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Diagnostics;

public sealed class DatabaseLoggingLevelPolicyTests(PostgresDatabaseFixture postgres)
    : IClassFixture<PostgresDatabaseFixture>,
        IAsyncLifetime
{
    private const string Email = "ana@test.com";
    private const string RouteTemplate = "/api/users/{id}";
    private const string DecoratorCategory =
        "CodigoActivo.Application.Common.Messaging.LoggingQueryDecorator";

    private static readonly Guid Identifier = new("3f6a0f4c-1c8a-4a3f-9a6e-2f0b7d5c8e11");

    private readonly SystemClock clock = new(TimeZoneInfo.Utc);

    public async ValueTask InitializeAsync()
    {
        await LogTables.TruncateAsync(postgres);
        await using var database = new LogDatabase(postgres.ConnectionString);
        await using var connection = await database.OpenConnectionAsync(Ct);
        await new LogPartitionMaintainer(
            database,
            new LogRetentionOptions(),
            clock
        ).EnsurePartitionsAsync(
            connection,
            LogTable.Diagnostics,
            [LogPartitions.DayOf(clock.UtcNow), LogPartitions.DayOf(clock.UtcNow).AddDays(1)],
            Ct
        );
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<List<DiagnosticLogEntry>> EntriesAfterRequestAsync(
        Func<ILoggerFactory, IResult> endpoint
    )
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Production,
            }
        );
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_ => new LogDatabase(postgres.ConnectionString));
        builder.Services.AddSingleton<IClock>(clock);
        builder.Services.AddSingleton(new LogRetentionOptions());
        builder.Services.AddSingleton<LogPartitionMaintainer>();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();
        ApiLogging.Configure(builder.Logging, builder.Environment);

        await using (var app = builder.Build())
        {
            app.UseExceptionHandler();
            app.MapGet(RouteTemplate, endpoint);
            await app.StartAsync(Ct);

            using var client = new HttpClient();
            using var response = await client.GetAsync(
                new Uri($"{app.Urls.First()}/api/users/{Identifier}?email={Email}"),
                Ct
            );

            await app.StopAsync(Ct);
        }

        return await LogTables.DiagnosticEntriesAsync(postgres);
    }

    private static string Written(IEnumerable<DiagnosticLogEntry> entries)
    {
        return string.Join(
            '\n',
            entries.Select(entry =>
                $"{entry.Category} {entry.EventName} {entry.Message} {entry.Properties} {entry.Exception}"
            )
        );
    }

    [Fact]
    public async Task RequestCarryingPersonalDataInTheUrlLeavesNoneOfItInTheTable()
    {
        var entries = await EntriesAfterRequestAsync(factory =>
        {
            factory.CreateLogger(DecoratorCategory).SlowUseCase("GetUserQuery", 5432);
            return Results.Unauthorized();
        });

        entries.Should().ContainSingle().Which.Message.Should().Be("GetUserQuery took 5432 ms");

        var written = Written(entries);
        written.Should().NotContain(Identifier.ToString());
        written.Should().NotContain(Email);
        written.Should().NotContain("/api/users/");
    }

    [Fact]
    public async Task RequestFailingWithAnExceptionIsRecordedOnlyByTheApiHandler()
    {
        var entries = await EntriesAfterRequestAsync(_ =>
            throw new InvalidOperationException("boom")
        );

        var failure = entries.Should().ContainSingle().Subject;
        failure.Category.Should().Be(typeof(GlobalExceptionHandler).FullName);
        failure.Level.Should().Be((short)LogLevel.Error);
        failure.Message.Should().Be($"Unhandled exception while processing GET {RouteTemplate}");
        failure.Exception.Should().StartWith("System.InvalidOperationException: boom");
        failure.TraceId.Should().MatchRegex("^[0-9a-f]{32}$");

        var written = Written(entries);
        written.Should().NotContain(Identifier.ToString());
        written.Should().NotContain(Email);
    }
}
