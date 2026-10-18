using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class PasswordLockoutWiringTests
{
    [Fact]
    public void AddCodigoActivoRegistersThePasswordAttemptGuardPerScope()
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
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Substitute.For<ICurrentUser>())
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<PasswordAttemptGuard>().Should().NotBeNull();
    }

    [Fact]
    public void DefaultMaxFailedAttemptsIsFive()
    {
        PasswordLockoutOptions.DefaultMaxFailedAttempts.Should().Be(5);
    }
}
