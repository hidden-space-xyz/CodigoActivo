using System.Security.Cryptography;
using System.Xml.Linq;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1.EdEC;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class Ed25519DataProtectionTests : IDisposable
{
    private const string Password = "a-strong-ed25519-certificate-password";

    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "codigoactivo-ed25519-tests",
        Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void LoadOrCreateCreatesEd25519CertificateAndReloadsPrivateKey()
    {
        var store = Ed25519CertificateStore.LoadOrCreate(
            new DirectoryInfo(directory),
            Password
        );

        var reloaded = Ed25519CertificateStore.LoadOrCreate(
            new DirectoryInfo(directory),
            Password
        );

        store.Certificate.SigAlgOid.Should().Be(EdECObjectIdentifiers.id_Ed25519.Id);
        reloaded.Certificate.GetEncoded().Should().Equal(store.Certificate.GetEncoded());
        Directory.GetFiles(directory).Should().HaveCount(2);
    }

    [Fact]
    public void EncryptRoundTripUsesAuthenticatedEd25519Envelope()
    {
        var store = Ed25519CertificateStore.LoadOrCreate(
            new DirectoryInfo(directory),
            Password
        );
        var plaintext = new XElement("secret", new XAttribute("id", "key-1"), "value");
        var encrypted = new Ed25519XmlEncryptor(store).Encrypt(plaintext);
        using var services = new ServiceCollection()
            .AddSingleton(store)
            .BuildServiceProvider();

        var decrypted = new Ed25519XmlDecryptor(services).Decrypt(
            encrypted.EncryptedElement
        );

        XNode.DeepEquals(decrypted, plaintext).Should().BeTrue();
        encrypted.EncryptedElement.ToString().Should().NotContain("value");
    }

    [Fact]
    public void LoadWithWrongPasswordFailsClosed()
    {
        Ed25519CertificateStore.LoadOrCreate(new DirectoryInfo(directory), Password);

        var act = () =>
            Ed25519CertificateStore.LoadOrCreate(
                new DirectoryInfo(directory),
                "a-different-strong-certificate-password"
            );

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DataProtectionProviderPersistsAndReloadsEd25519ProtectedKeyRing()
    {
        var certificateDirectory = new DirectoryInfo(Path.Combine(directory, "certificate"));
        var keysDirectory = new DirectoryInfo(Path.Combine(directory, "keys"));
        var store = Ed25519CertificateStore.LoadOrCreate(certificateDirectory, Password);
        string protectedValue;
        using (var services = BuildDataProtectionProvider(store, keysDirectory))
        {
            protectedValue = services
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("test")
                .Protect("sensitive-value");
        }

        var keyFile = keysDirectory.GetFiles("key-*.xml").Should().ContainSingle().Subject;
        var persistedXml = File.ReadAllText(keyFile.FullName);
        persistedXml.Should().Contain("urn:codigoactivo:data-protection:ed25519:v1");
        persistedXml.Should().NotContain("sensitive-value");

        var reloadedStore = Ed25519CertificateStore.LoadOrCreate(
            certificateDirectory,
            Password
        );
        using var reloadedServices = BuildDataProtectionProvider(reloadedStore, keysDirectory);
        var plaintext = reloadedServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test")
            .Unprotect(protectedValue);

        plaintext.Should().Be("sensitive-value");
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ServiceProvider BuildDataProtectionProvider(
        Ed25519CertificateStore store,
        DirectoryInfo keysDirectory
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo.Tests")
            .PersistKeysToFileSystem(keysDirectory);
        services.Configure<KeyManagementOptions>(options =>
            options.XmlEncryptor = new Ed25519XmlEncryptor(store)
        );
        return services.BuildServiceProvider();
    }
}
