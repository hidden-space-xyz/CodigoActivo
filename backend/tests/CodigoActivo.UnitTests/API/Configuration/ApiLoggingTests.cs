using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Composition.Diagnostics;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Configuration;

public sealed class ApiLoggingTests
{
    private const string HostingCategory = "Microsoft.AspNetCore.Hosting.Diagnostics";
    private const string DatabaseCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";
    private const string SaveChangesCategory = "Microsoft.EntityFrameworkCore.Update";
    private const string QueryCategory = "Microsoft.EntityFrameworkCore.Query";
    private const string DecoratorCategory =
        "CodigoActivo.Application.Common.Messaging.LoggingQueryDecorator";

    private static List<Type?> Providers(string environment)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => ApiLogging.Configure(logging, Environment(environment)));

        return
        [
            .. services
                .Where(descriptor => descriptor.ServiceType == typeof(ILoggerProvider))
                .Select(descriptor => descriptor.ImplementationType),
        ];
    }

    private static IHostEnvironment Environment(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(name);
        return environment;
    }

    [Fact]
    public void ConfigureInProductionStoresEventsOnlyInTheDatabase()
    {
        Providers(Environments.Production)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be<DatabaseLoggerProvider>();
    }

    [Fact]
    public void ConfigureInDevelopmentAlsoWritesToTheConsole()
    {
        Providers(Environments.Development)
            .Should()
            .BeEquivalentTo([typeof(DatabaseLoggerProvider), typeof(ConsoleLoggerProvider)]);
    }

    [Fact]
    public void ConfigureLetsTheConfiguredLevelsDecideWhatTheDatabaseKeeps()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
        configuration["Logging:LogLevel:Default"].Should().Be("Warning");

        var services = new ServiceCollection();
        services.AddSingleton(new LogDatabase("Host=127.0.0.1;Port=1;Database=unused"));
        services.AddSingleton(Substitute.For<IClock>());
        services.AddSingleton(new LogRetentionOptions());
        services.AddSingleton<LogPartitionMaintainer>();
        services.AddLogging(logging =>
        {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            ApiLogging.Configure(logging, Environment(Environments.Production));
        });
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();

        factory
            .CreateLogger(HostingCategory)
            .IsEnabled(LogLevel.Information)
            .Should()
            .BeFalse("the request pipeline events carry the path and the query string");
        factory
            .CreateLogger(DatabaseCommandCategory)
            .IsEnabled(LogLevel.Error)
            .Should()
            .BeFalse("a failed command is reported with its whole SQL statement");
        factory
            .CreateLogger(SaveChangesCategory)
            .IsEnabled(LogLevel.Error)
            .Should()
            .BeFalse("a failed save is rethrown and recorded where it is caught");
        factory
            .CreateLogger(QueryCategory)
            .IsEnabled(LogLevel.Warning)
            .Should()
            .BeFalse("query compilation hints are no failures");
        factory
            .CreateLogger("Microsoft.Hosting.Lifetime")
            .IsEnabled(LogLevel.Information)
            .Should()
            .BeFalse("a start or a stop that succeeds is normal operation");
        factory
            .CreateLogger(LogCategories.Lifecycle)
            .IsEnabled(LogLevel.Critical)
            .Should()
            .BeTrue();
        factory.CreateLogger(DecoratorCategory).IsEnabled(LogLevel.Information).Should().BeFalse();
        factory.CreateLogger(DecoratorCategory).IsEnabled(LogLevel.Warning).Should().BeTrue();
    }

    [Fact]
    public void RecordFatalWithABuiltHostAlsoWritesThroughItsProviders()
    {
        var recorder = new RecordingLoggerProvider();
        using var host = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(recorder))
            .BuildServiceProvider();
        using var logging = new ApiLogging();

        logging.RecordFatal(new InvalidOperationException("boom"), host);

        recorder
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Be($"Critical {LogCategories.Lifecycle}: The API host terminated unexpectedly boom");
    }

    [Fact]
    public void RecordFatalBeforeTheHostIsBuiltOnlyNeedsTheConsole()
    {
        using var logging = new ApiLogging();

        var record = () => logging.RecordFatal(new InvalidOperationException("boom"), host: null);

        record.Should().NotThrow();
    }
}
