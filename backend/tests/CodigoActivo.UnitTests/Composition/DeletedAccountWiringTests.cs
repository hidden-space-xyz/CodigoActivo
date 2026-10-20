using AwesomeAssertions;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Users;
using CodigoActivo.Domain.Users.Erasure;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class DeletedAccountWiringTests
{
    private static IServiceCollection Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                }
            )
            .Build();
        return new ServiceCollection().AddLogging().AddCodigoActivo(configuration);
    }

    [Fact]
    public void AddCodigoActivoRegistersThePurgerAsAHostedService()
    {
        using var provider = Build().BuildServiceProvider();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is DeletedAccountPurgeWorker);
    }

    [Fact]
    public void AddCodigoActivoErasesUnverifiedAccountsHourlyAfterTheStartupDelay()
    {
        using var provider = Build().BuildServiceProvider();
        var options = provider.GetRequiredService<UnverifiedAccountPurgeOptions>();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is UnverifiedAccountPurgeWorker);
        options.Interval.Should().Be(TimeSpan.FromHours(1));
        options.StartupDelay.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AddCodigoActivoRegistersTheRepositoryPerScope()
    {
        Build()
            .Should()
            .ContainSingle(descriptor =>
                descriptor.ServiceType == typeof(IDeletedAccountRepository)
                && descriptor.ImplementationType == typeof(DeletedAccountRepository)
                && descriptor.Lifetime == ServiceLifetime.Scoped
            );
    }

    [Fact]
    public void AddCodigoActivoPurgesHourlyAfterTheStartupDelay()
    {
        using var provider = Build().BuildServiceProvider();
        var options = provider.GetRequiredService<DeletedAccountPurgeOptions>();

        options.Interval.Should().Be(TimeSpan.FromHours(1));
        options.StartupDelay.Should().Be(TimeSpan.FromSeconds(30));
    }
}
