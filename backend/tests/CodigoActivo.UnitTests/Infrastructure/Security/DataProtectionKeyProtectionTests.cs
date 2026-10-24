using System.Security.Cryptography;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class DataProtectionKeyProtectionTests : IDisposable
{
    private readonly DataProtectionRootKey rootKey = DataProtectionRootKeys.Random();
    private readonly DirectoryInfo keysDirectory = new(
        Path.Join(Path.GetTempPath(), "codigoactivo-keyring-tests", Guid.NewGuid().ToString("N"))
    );

    [Fact]
    public void ProtectKeysWithRootKeyPersistsAnEncryptedKeyRingThatReloads()
    {
        var protectedValue = Protect(rootKey, "session-payload");

        var keyFile = keysDirectory.GetFiles("key-*.xml").Should().ContainSingle().Subject;
        var persistedXml = File.ReadAllText(keyFile.FullName);
        persistedXml.Should().Contain("urn:codigoactivo:data-protection:aes-gcm:v1");
        persistedXml.Should().Contain(typeof(AesGcmXmlDecryptor).FullName);
        persistedXml.Should().Contain("AES_256_GCM");
        persistedXml.Should().NotContain("masterKey");

        using var reloaded = BuildProvider(rootKey);
        reloaded
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("session")
            .Unprotect(protectedValue)
            .Should()
            .Be("session-payload");
    }

    [Fact]
    public void ProtectKeysWithRootKeyAnotherRootKeyStopsTheRegistration()
    {
        Protect(rootKey, "session-payload");

        var act = () => BuildProvider(DataProtectionRootKeys.Random());

        act.Should()
            .Throw<CryptographicException>()
            .WithMessage("DATA_PROTECTION_KEY does not decrypt the key ring*");
        keysDirectory.GetFiles("key-*.xml").Should().ContainSingle();
    }

    [Fact]
    public void ProtectPayloadsWithAesGcmConfiguresAes256GcmForEveryEnvironment()
    {
        var services = new ServiceCollection();
        var dataProtection = services.AddDataProtection().SetApplicationName("CodigoActivo");

        dataProtection.ProtectPayloadsWithAesGcm();

        using var provider = services.BuildServiceProvider();
        provider
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value.AuthenticatedEncryptorConfiguration.Should()
            .BeOfType<AuthenticatedEncryptorConfiguration>()
            .Which.EncryptionAlgorithm.Should()
            .Be(EncryptionAlgorithm.AES_256_GCM);

        var protector = provider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("payload");
        protector.Unprotect(protector.Protect("payload-value")).Should().Be("payload-value");
        FluentActions
            .Invoking(() => DataProtectionKeyProtection.ProtectPayloadsWithAesGcm(null!))
            .Should()
            .Throw<ArgumentNullException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(keysDirectory.FullName))
        {
            keysDirectory.Delete(recursive: true);
        }
    }

    private string Protect(DataProtectionRootKey key, string plaintext)
    {
        using var provider = BuildProvider(key);
        return provider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("session")
            .Protect(plaintext);
    }

    private ServiceProvider BuildProvider(DataProtectionRootKey key)
    {
        var services = new ServiceCollection();
        services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .ProtectPayloadsWithAesGcm()
            .ProtectKeysWithRootKey(keysDirectory, key);
        return services.BuildServiceProvider();
    }
}
