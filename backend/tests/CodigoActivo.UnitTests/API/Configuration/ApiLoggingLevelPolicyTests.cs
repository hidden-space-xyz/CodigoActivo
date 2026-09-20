using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.API.Diagnostics;
using CodigoActivo.API.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.API.Configuration;

public sealed class ApiLoggingLevelPolicyTests : IDisposable
{
    private const string Email = "ana@test.com";
    private const string RouteTemplate = "/api/users/{id}";
    private const string CsrfCategory = "CodigoActivo.API.Middlewares.CsrfValidationMiddleware";
    private const string HostingCategory = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string DatabaseCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    private static readonly Guid Identifier = new("3f6a0f4c-1c8a-4a3f-9a6e-2f0b7d5c8e11");

    private readonly string directory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-tests",
        Guid.NewGuid().ToString("N")
    );

    private WebApplicationBuilder HostBuilder()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
                EnvironmentName = Environments.Production,
            }
        );

        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { [ApiLogging.DirectoryKey] = directory }
        );
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        return builder;
    }

    private IReadOnlyList<string> WrittenLines()
    {
        return
        [
            .. Directory
                .EnumerateFiles(directory)
                .SelectMany(File.ReadAllLines)
                .Where(line => !string.IsNullOrWhiteSpace(line)),
        ];
    }

    private static IReadOnlyList<string> Categories(IReadOnlyList<string> lines)
    {
        return
        [
            .. lines
                .Select(line => line.Split(' ', 4))
                .Where(parts => parts.Length >= 3)
                .Select(parts => parts[2]),
        ];
    }

    private async Task<IReadOnlyList<string>> LinesAfterRequestAsync(
        Func<ILoggerFactory, IResult> endpoint
    )
    {
        var builder = HostBuilder();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        using (var logging = ApiLogging.Start(builder.Configuration))
        {
            logging.Configure(builder.Logging);

            await using var app = builder.Build();
            app.UseExceptionHandler();
            app.MapGet(RouteTemplate, endpoint);

            await app.StartAsync(TestContext.Current.CancellationToken);

            using var client = new HttpClient();
            using var response = await client.GetAsync(
                new Uri($"{app.Urls.First()}/api/users/{Identifier}?email={Email}"),
                TestContext.Current.CancellationToken
            );

            await app.StopAsync(TestContext.Current.CancellationToken);
        }

        return WrittenLines();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConfigureLetsTheConfiguredLevelsDecideWhatTheFileKeeps()
    {
        var builder = HostBuilder();
        builder.Configuration["Logging:LogLevel:Default"].Should().Be("Warning");

        using var logging = ApiLogging.Start(builder.Configuration);
        logging.Configure(builder.Logging);

        using var app = builder.Build();
        var factory = app.Services.GetRequiredService<ILoggerFactory>();

        factory
            .CreateLogger(HostingCategory)
            .IsEnabled(LogLevel.Information)
            .Should()
            .BeFalse("the request pipeline events carry the path and the query string");
        factory
            .CreateLogger(DatabaseCommandCategory)
            .IsEnabled(LogLevel.Information)
            .Should()
            .BeFalse("the executed SQL quotes the stored data");
        factory
            .CreateLogger(DatabaseCommandCategory)
            .IsEnabled(LogLevel.Error)
            .Should()
            .BeFalse("a failed command is reported with its whole SQL statement");
        factory
            .CreateLogger(LogCategories.Lifecycle)
            .IsEnabled(LogLevel.Information)
            .Should()
            .BeTrue();
        factory
            .CreateLogger("Microsoft.Hosting.Lifetime")
            .IsEnabled(LogLevel.Information)
            .Should()
            .BeTrue();
        factory.CreateLogger(CsrfCategory).IsEnabled(LogLevel.Information).Should().BeFalse();
        factory.CreateLogger(CsrfCategory).IsEnabled(LogLevel.Warning).Should().BeTrue();
    }

    [Fact]
    public async Task RequestCarryingPersonalDataInTheUrlLeavesNoneOfItInTheFile()
    {
        var lines = await LinesAfterRequestAsync(factory =>
        {
            factory
                .CreateLogger(CsrfCategory)
                .CsrfValidationFailed(new InvalidOperationException("rejected"));
            return Results.Unauthorized();
        });

        lines
            .Should()
            .Contain(line => line.Contains("CSRF validation failed", StringComparison.Ordinal));

        var written = string.Join('\n', lines);
        written.Should().NotContain(Identifier.ToString());
        written.Should().NotContain(Email);
        written.Should().NotContain("Request starting");
        written.Should().NotContain("Request finished");
        written.Should().NotContain($"/api/users/{Identifier}");
    }

    [Fact]
    public async Task RequestFailingWithAnExceptionIsRecordedOnlyByTheApiHandler()
    {
        var lines = await LinesAfterRequestAsync(_ => throw new InvalidOperationException("boom"));

        var failures = lines
            .Where(line => line.Contains(" ERR ", StringComparison.Ordinal))
            .ToList();

        failures
            .Should()
            .ContainSingle()
            .Which.Should()
            .Contain("CodigoActivo.API.Middlewares.GlobalExceptionHandler")
            .And.Contain($"Unhandled exception while processing GET {RouteTemplate}");

        Categories(lines)
            .Should()
            .NotContain(category =>
                category.StartsWith("Microsoft.AspNetCore.Diagnostics", StringComparison.Ordinal)
            );

        var written = string.Join('\n', lines);
        written.Should().NotContain(Identifier.ToString());
        written.Should().NotContain(Email);
    }
}
