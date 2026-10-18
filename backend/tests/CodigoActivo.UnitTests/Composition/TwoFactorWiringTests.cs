using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class TwoFactorWiringTests : IDisposable
{
    private readonly ServiceProvider provider;

    public TwoFactorWiringTests()
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
        provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        provider.Dispose();
    }

    [Fact]
    public void AddCodigoActivoRegistersTheTotpAndSecretProtectionPorts()
    {
        provider.GetRequiredService<ITotpService>().Should().BeOfType<TotpService>();
        var protector = provider.GetRequiredService<ISecretProtector>();
        protector.Should().BeOfType<DataProtectionSecretProtector>();
        protector.TryUnprotect(protector.Protect("secret"), out var plaintext).Should().BeTrue();
        plaintext.Should().Be("secret");
    }

    [Fact]
    public void AddCodigoActivoRegistersTheKeyedOneTimeCodeHasher()
    {
        var hasher = provider.GetRequiredService<IOneTimeCodeHasher>();
        hasher.Should().BeOfType<HmacOneTimeCodeHasher>();
        hasher.Verify("123456", hasher.Hash("123456")).Should().BeTrue();
    }
}
