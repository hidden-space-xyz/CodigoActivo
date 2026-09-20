using AwesomeAssertions;
using CodigoActivo.API.Diagnostics;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.API.Diagnostics;

public sealed class LogFileRetentionCleanerTests : IDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Expired = TimeSpan.FromDays(13) + TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Kept = TimeSpan.FromDays(12) + TimeSpan.FromHours(23);

    private readonly string directory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly TestClock clock = new();
    private readonly RecordingLogger<LogFileRetentionCleaner> logger = new();

    public LogFileRetentionCleanerTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private LogFileRetentionCleaner Build(Action<string>? removeFile = null)
    {
        return new LogFileRetentionCleaner(directory, clock, removeFile ?? File.Delete, logger);
    }

    private string WriteFile(string name, TimeSpan age)
    {
        var path = Path.Join(directory, name);
        File.WriteAllText(path, "entry");
        File.SetLastWriteTimeUtc(path, clock.UtcNow.UtcDateTime - age);
        return path;
    }

    private static IServiceCollection Services(string? logDirectory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["LOG_DIRECTORY"] = logDirectory }
            )
            .Build();

        var services = new ServiceCollection().AddLogging().AddSingleton<IClock>(new TestClock());
        LogFileRetentionCleaner.AddLogFileRetention(services, configuration);
        return services;
    }

    [Fact]
    public void SweepRemovesTheDailyFilesOlderThanTheRetentionWindow()
    {
        var expired = WriteFile("api-20250101.log", Expired);
        var rolled = WriteFile("api-20250101_001.log", Expired);

        Build().Sweep();

        File.Exists(expired).Should().BeFalse();
        File.Exists(rolled).Should().BeFalse();
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void SweepKeepsTheDailyFilesInsideTheRetentionWindow()
    {
        var recent = WriteFile("api-20260101.log", Kept);
        var rolled = WriteFile("api-20260101_002.log", Kept);
        var current = WriteFile("api-20260704.log", TimeSpan.Zero);

        Build().Sweep();

        File.Exists(recent).Should().BeTrue();
        File.Exists(rolled).Should().BeTrue();
        File.Exists(current).Should().BeTrue();
    }

    [Fact]
    public void SweepKeepsEveryFileTheSinkDidNotWrite()
    {
        var names = new[]
        {
            "error.log",
            "other-20260101.log",
            "api-notes.txt",
            "api-notes.log",
            "api-2025010.log",
            "api-20250101.log.gz",
        };
        var kept = names.Select(name => WriteFile(name, Expired)).ToList();
        var subdirectory = Path.Join(directory, "api-20250102.log");
        Directory.CreateDirectory(subdirectory);

        Build().Sweep();

        foreach (var path in kept)
        {
            File.Exists(path).Should().BeTrue(path);
        }

        Directory.Exists(subdirectory).Should().BeTrue();
    }

    [Fact]
    public void SweepLogsTheFailedRemovalAndContinuesWithTheRemainingFiles()
    {
        var blocked = WriteFile("api-20250101.log", Expired);
        var removable = WriteFile("api-20250102.log", Expired);

        Build(path =>
            {
                if (string.Equals(path, blocked, StringComparison.Ordinal))
                {
                    throw new IOException("the file is in use");
                }

                File.Delete(path);
            })
            .Sweep();

        File.Exists(blocked).Should().BeTrue();
        File.Exists(removable).Should().BeFalse();
        logger
            .LevelEntries.Should()
            .ContainSingle()
            .Which.Should()
            .Match<(LogLevel Level, string Message)>(entry =>
                entry.Level == LogLevel.Warning
                && entry.Message.Contains(
                    "Expired log file removal failed",
                    StringComparison.Ordinal
                )
                && entry.Message.Contains("the file is in use", StringComparison.Ordinal)
            );
    }

    [Fact]
    public void SweepOnAnEmptyOrMissingDirectoryDoesNothing()
    {
        Build().Sweep();

        Directory.Delete(directory, recursive: true);
        Build().Sweep();

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncSweepsAtStartupWithoutWaitingForTheFirstInterval()
    {
        var expired = WriteFile("api-20250101.log", Expired);
        var recent = WriteFile("api-20260704.log", TimeSpan.Zero);
        var signal = new TaskCompletionSource();
        var cleaner = Build(path =>
        {
            File.Delete(path);
            signal.TrySetResult();
        });

        await cleaner.StartAsync(TestContext.Current.CancellationToken);
        await signal.Task.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        await cleaner.StopAsync(TestContext.Current.CancellationToken);

        File.Exists(expired).Should().BeFalse();
        File.Exists(recent).Should().BeTrue();
        cleaner.ExecuteTask!.IsFaulted.Should().BeFalse();
        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsyncStopsCleanlyWhenTheHostShutsDown()
    {
        var cleaner = Build();

        await cleaner.StartAsync(TestContext.Current.CancellationToken);
        await cleaner.StopAsync(TestContext.Current.CancellationToken);

        cleaner.ExecuteTask!.IsCompleted.Should().BeTrue();
        cleaner.ExecuteTask.IsFaulted.Should().BeFalse();
        logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLogFileRetentionRegistersNothingWithoutALogDirectory(string? logDirectory)
    {
        using var provider = Services(logDirectory).BuildServiceProvider();

        provider.GetServices<IHostedService>().Should().BeEmpty();
    }

    [Fact]
    public void AddLogFileRetentionRegistersTheCleanerWithALogDirectory()
    {
        using var provider = Services(directory).BuildServiceProvider();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<LogFileRetentionCleaner>();
    }
}
