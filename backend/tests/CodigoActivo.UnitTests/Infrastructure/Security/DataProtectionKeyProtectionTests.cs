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
    private readonly string keysDirectory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-keyring-tests",
        Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void ProtectKeysWithRootKeyPersistsAnEncryptedKeyRingThatReloads()
    {
        var protectedValue = Protect(rootKey, "session-payload");

        var keyFile = Directory
            .GetFiles(keysDirectory, "key-*.xml")
            .Should()
            .ContainSingle()
            .Subject;
        var persistedXml = File.ReadAllText(keyFile);
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
    public void VerifyKeyRingOtherRootKeyThrows()
    {
        Protect(rootKey, "session-payload");

        new AesGcmKeyRingProtection(rootKey)
            .Invoking(protection => protection.VerifyKeyRing(keysDirectory))
            .Should()
            .NotThrow();
        new AesGcmKeyRingProtection(DataProtectionRootKeys.Random())
            .Invoking(protection => protection.VerifyKeyRing(keysDirectory))
            .Should()
            .Throw<CryptographicException>()
            .WithMessage("DATA_PROTECTION_KEY does not decrypt the Data Protection key ring.");
    }

    [Fact]
    public void VerifyKeyRingMissingDirectoryPasses()
    {
        new AesGcmKeyRingProtection(rootKey)
            .Invoking(protection => protection.VerifyKeyRing(keysDirectory))
            .Should()
            .NotThrow();
    }

    [Fact]
    public void ProtectPayloadsWithAesGcmConfiguresAes256GcmForEveryEnvironment()
    {
        var services = new ServiceCollection();
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

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
        if (Directory.Exists(keysDirectory))
        {
            Directory.Delete(keysDirectory, recursive: true);
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
        services.AddSingleton(key);
        services.AddSingleton(new KeyRingOptions { DirectoryPath = keysDirectory });
        services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .ProtectPayloadsWithAesGcm()
            .ProtectKeysWithRootKey();
        return services.BuildServiceProvider();
    }
}
