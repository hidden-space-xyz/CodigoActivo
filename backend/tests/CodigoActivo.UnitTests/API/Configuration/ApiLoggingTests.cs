using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.API.Diagnostics;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Composition.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.API.Configuration;

public sealed class ApiLoggingTests : IDisposable
{
    private const string DecoratorCategory =
        "CodigoActivo.Application.Common.Messaging.LoggingCommandDecorator";

    private readonly string directory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-tests",
        Guid.NewGuid().ToString("N")
    );

    private static IConfiguration Configuration(string? logDirectory)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["LOG_DIRECTORY"] = logDirectory }
            )
            .Build();
    }

    private static IReadOnlyList<string> Providers(ApiLogging logging)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging.Configure);
        using var provider = services.BuildServiceProvider();

        return
        [
            .. provider
                .GetServices<ILoggerProvider>()
                .Select(candidate => candidate.GetType().Name),
        ];
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

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StartWithoutALogDirectoryWritesToTheConsoleOnly(string? configured)
    {
        using var logging = ApiLogging.Start(Configuration(configured));

        Providers(logging).Should().ContainSingle().Which.Should().Be("ConsoleLoggerProvider");
    }

    [Fact]
    public void StartWithALogDirectoryWritesToTheFileOnly()
    {
        using var logging = ApiLogging.Start(Configuration(directory));

        Providers(logging).Should().ContainSingle().Which.Should().Be("SerilogLoggerProvider");
    }

    [Fact]
    public void StartWithALogDirectoryThrowsWhenTheDailyFileCannotBeOpened()
    {
        Directory.CreateDirectory(Path.Join(directory, $"api-{DateTime.Now:yyyyMMdd}.log"));

        var start = () => ApiLogging.Start(Configuration(directory));

        start
            .Should()
            .Throw<InvalidOperationException>("a process without any log must not keep running")
            .WithMessage($"*{directory}*");
    }

    [Fact]
    public void StartWithALogDirectoryWritesOneDailyFileNamedAfterTheDate()
    {
        using (var logging = ApiLogging.Start(Configuration(directory)))
        {
            logging.CreateLogger(DecoratorCategory).SlowUseCase("UpdateUserCommand", 5432);
        }

        var file = Directory.EnumerateFiles(directory).Should().ContainSingle().Subject;
        Path.GetFileName(file)
            .Should()
            .MatchRegex(@"^api-\d{8}\.log$", "the sink rolls once per day");
    }

    [Fact]
    public void StartWithALogDirectoryWritesOneLinePerMessageWithTheUtcTimestampLevelAndCategory()
    {
        using (var logging = ApiLogging.Start(Configuration(directory)))
        {
            var logger = logging.CreateLogger(DecoratorCategory);
            logger.SlowUseCase("UpdateUserCommand", 5432);
            logger.SlowUseCase("DeleteUserCommand", 6543);
        }

        var lines = WrittenLines();
        lines.Should().HaveCount(2);
        lines[0]
            .Should()
            .MatchRegex(
                @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z WRN CodigoActivo\.Application\.Common\.Messaging\.LoggingCommandDecorator UpdateUserCommand took 5432 ms$"
            );
        lines[1].Should().EndWith("DeleteUserCommand took 6543 ms");
    }

    [Fact]
    public void StartWithALogDirectoryKeepsTheFatalStartupEventWhenTheConfigurationSilencesEveryLevel()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [ApiLogging.DirectoryKey] = directory,
                    ["Logging:LogLevel:Default"] = "None",
                    [$"Logging:LogLevel:{LogCategories.Lifecycle}"] = "None",
                }
            )
            .Build();

        var failure = Failure();

        using (var logging = ApiLogging.Start(configuration))
        {
            logging.CreateLogger(LogCategories.Lifecycle).HostTerminatedUnexpectedly(failure);
        }

        var lines = WrittenLines();
        lines[0]
            .Should()
            .EndWith($"FTL {LogCategories.Lifecycle} The API host terminated unexpectedly");
        lines.Should().Contain("System.InvalidOperationException: boom");
    }

    [Fact]
    public void StartWithALogDirectoryWritesTheExceptionOfAnEventBelowItsMessage()
    {
        using (var logging = ApiLogging.Start(Configuration(directory)))
        {
            logging
                .CreateLogger("CodigoActivo.API.Middlewares.GlobalExceptionHandler")
                .UnhandledRequestException("DELETE", "api/users/{id}", Failure());
        }

        var lines = WrittenLines();
        lines[0]
            .Should()
            .EndWith(
                "ERR CodigoActivo.API.Middlewares.GlobalExceptionHandler "
                    + "Unhandled exception while processing DELETE api/users/{id}"
            );
        lines[1].Should().Be("System.InvalidOperationException: boom");
        lines[2].Should().Be("second line");
    }

    private static InvalidOperationException Failure()
    {
        try
        {
            throw new InvalidOperationException("boom\nsecond line");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }
}
