using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class TwoFactorWiringTests : IDisposable
{
    private readonly string keysDirectory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-keyring-tests",
        Guid.NewGuid().ToString("N")
    );
    private readonly string dataProtectionKey = DataProtectionRootKeys.RandomBase64();
    private readonly ServiceProvider provider;

    public TwoFactorWiringTests()
    {
        provider = BuildProvider(dataProtectionKey);
    }

    public void Dispose()
    {
        provider.Dispose();
        if (Directory.Exists(keysDirectory))
        {
            Directory.Delete(keysDirectory, recursive: true);
        }
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

    [Fact]
    public void AddCodigoActivoDerivesTheOneTimeCodeKeyFromTheDataProtectionKey()
    {
        var hash = provider.GetRequiredService<IOneTimeCodeHasher>().Hash("123456");

        using var restarted = BuildProvider(dataProtectionKey);
        using var rekeyed = BuildProvider(DataProtectionRootKeys.RandomBase64());

        restarted.GetRequiredService<IOneTimeCodeHasher>().Verify("123456", hash).Should().BeTrue();
        rekeyed.GetRequiredService<IOneTimeCodeHasher>().Verify("123456", hash).Should().BeFalse();
    }

    private ServiceProvider BuildProvider(string key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                    ["DATA_PROTECTION_KEY"] = key,
                }
            )
            .Build();
        return new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .AddSingleton(new KeyRingOptions { DirectoryPath = keysDirectory })
            .BuildServiceProvider();
    }
}
