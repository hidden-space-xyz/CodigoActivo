using System.Security.Cryptography;
using System.Xml.Linq;
using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.API.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.EdEC;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class Ed25519DataProtectionTests : IDisposable
{
    private const string Password = "a-strong-ed25519-certificate-password";
    private const string OtherPassword = "a-different-strong-certificate-password";
    private const string CertificateFileName = "key-encryption-ed25519.cer";
    private const string PrivateKeyFileName = "key-encryption-ed25519.key";
    private const string CmsNamespace = "urn:codigoactivo:data-protection:cms:v2";
    private const string LegacyNamespace = "urn:codigoactivo:data-protection:ed25519:v1";

    private static readonly string FixtureDirectory = Path.Join(
        AppContext.BaseDirectory,
        "TestData",
        "Ed25519V1"
    );

    private readonly string directory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-ed25519-tests",
        Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void LoadOrCreateWritesPkcs8EncryptedPrivateKeyInfo()
    {
        var store = Load(directory);
        var reloaded = Load(directory);

        store.Certificate.SigAlgOid.Should().Be(EdECObjectIdentifiers.id_Ed25519.Id);
        reloaded.Certificate.GetEncoded().Should().Equal(store.Certificate.GetEncoded());
        Directory.GetFiles(directory).Should().HaveCount(2);

        var encrypted = EncryptedPrivateKeyInfo.GetInstance(
            Asn1Object.FromByteArray(File.ReadAllBytes(Path.Join(directory, PrivateKeyFileName)))
        );
        encrypted.EncryptionAlgorithm.Algorithm.Should().Be(PkcsObjectIdentifiers.IdPbeS2);

        var parameters = PbeS2Parameters.GetInstance(encrypted.EncryptionAlgorithm.Parameters);
        parameters.KeyDerivationFunc.Algorithm.Should().Be(PkcsObjectIdentifiers.IdPbkdf2);
        parameters.EncryptionScheme.Algorithm.Should().Be(NistObjectIdentifiers.IdAes256Cbc);

        var derivation = Pbkdf2Params.GetInstance(parameters.KeyDerivationFunc.Parameters);
        derivation.IterationCount.Should().Be(BigInteger.ValueOf(600_000));
        derivation.Prf.Algorithm.Should().Be(PkcsObjectIdentifiers.IdHmacWithSha512);
        derivation.GetSalt().Should().HaveCount(32);
    }

    [Fact]
    public void EncryptProducesSignedCmsEnvelopeThatRoundTrips()
    {
        var store = Load(directory);
        var plaintext = new XElement("secret", new XAttribute("id", "key-1"), "value");

        var encrypted = new Ed25519CmsXmlEncryptor(store).Encrypt(plaintext);
        var decrypted = Decryptor(store).Decrypt(encrypted.EncryptedElement);

        XNode.DeepEquals(decrypted, plaintext).Should().BeTrue();
        encrypted.DecryptorType.Should().Be<Ed25519CmsXmlDecryptor>();
        encrypted.EncryptedElement.Name.NamespaceName.Should().Be(CmsNamespace);
        encrypted.EncryptedElement.ToString().Should().NotContain("value");

        var blob = Convert.FromBase64String(encrypted.EncryptedElement.Value);
        var contentInfo = Org.BouncyCastle.Asn1.Cms.ContentInfo.GetInstance(
            Asn1Object.FromByteArray(blob)
        );
        contentInfo.ContentType.Should().Be(PkcsObjectIdentifiers.SignedData);

        var signedData = new CmsSignedData(contentInfo);
        var signer = signedData.GetSignerInfos().GetSigners().Should().ContainSingle().Subject;
        signer.DigestAlgorithmID.Algorithm.Should().Be(NistObjectIdentifiers.IdSha512);
        signer.SignatureAlgorithm.Algorithm.Should().Be(EdECObjectIdentifiers.id_Ed25519);
        signer.Verify(store.Certificate).Should().BeTrue();

        var enveloped = new CmsEnvelopedData(ReadSignedContent(signedData));
        enveloped.EncryptionAlgorithmID.Algorithm.Should().Be(NistObjectIdentifiers.IdAes256Cbc);

        var recipient = enveloped
            .GetRecipientInfos()
            .GetRecipients()
            .Should()
            .ContainSingle()
            .Subject.Should()
            .BeOfType<PasswordRecipientInformation>()
            .Subject;
        recipient
            .KeyEncryptionAlgorithmID.Algorithm.Should()
            .Be(PkcsObjectIdentifiers.IdAlgPwriKek);
        AlgorithmIdentifier
            .GetInstance(recipient.KeyEncryptionAlgorithmID.Parameters)
            .Algorithm.Should()
            .Be(NistObjectIdentifiers.IdAes256Cbc);
        recipient.KeyDerivationAlgorithm.Algorithm.Should().Be(PkcsObjectIdentifiers.IdPbkdf2);
        Pbkdf2Params
            .GetInstance(recipient.KeyDerivationAlgorithm.Parameters)
            .IterationCount.Should()
            .Be(BigInteger.ValueOf(600_000));
    }

    [Fact]
    public void LoadWithWrongPasswordFailsClosed()
    {
        Load(directory);

        var act = () => Load(directory, OtherPassword);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptWithWrongCertificatePasswordFailsClosed()
    {
        var store = Load(directory);
        var encrypted = new Ed25519CmsXmlEncryptor(store).Encrypt(new XElement("secret", "value"));
        var copy = Path.Join(directory, "copy");
        CopyDirectory(directory, copy);
        RewritePrivateKeyPassword(Path.Join(copy, PrivateKeyFileName));

        var act = () => Decryptor(Load(copy, OtherPassword)).Decrypt(encrypted.EncryptedElement);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptWithSwappedCertificateFailsClosed()
    {
        var store = Load(Path.Join(directory, "first"));
        var other = Load(Path.Join(directory, "second"));
        var encrypted = new Ed25519CmsXmlEncryptor(store).Encrypt(new XElement("secret", "value"));

        var act = () => Decryptor(other).Decrypt(encrypted.EncryptedElement);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptWithForeignEd25519SignatureFailsClosed()
    {
        var store = Load(directory);
        var encrypted = new Ed25519CmsXmlEncryptor(store).Encrypt(new XElement("secret", "value"));
        var content = ReadSignedContent(
            new CmsSignedData(Convert.FromBase64String(encrypted.EncryptedElement.Value))
        );
        var forged = new XElement(
            XNamespace.Get(CmsNamespace) + "encryptedSecret",
            new XAttribute("version", 2),
            Convert.ToBase64String(SignWithNewKey(content))
        );

        var act = () => Decryptor(store).Decrypt(forged);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptRejectsEverySingleByteChangeInTheCmsBlob()
    {
        var store = Load(directory);
        var decryptor = Decryptor(store);
        var encrypted = new Ed25519CmsXmlEncryptor(store).Encrypt(new XElement("secret", "value"));
        var blob = Convert.FromBase64String(encrypted.EncryptedElement.Value);
        var accepted = new List<int>();

        for (var offset = 0; offset < blob.Length; offset++)
        {
            var tampered = (byte[])blob.Clone();
            tampered[offset] ^= 0x01;
            var element = new XElement(
                XNamespace.Get(CmsNamespace) + "encryptedSecret",
                new XAttribute("version", 2),
                Convert.ToBase64String(tampered)
            );

            try
            {
                decryptor.Decrypt(element);
                accepted.Add(offset);
            }
            catch (CryptographicException)
            {
                continue;
            }
        }

        accepted.Should().BeEmpty();
    }

    [Fact]
    public void DecryptRejectsForeignNamespaceAndVersion()
    {
        var store = Load(directory);
        var decryptor = Decryptor(store);
        var encrypted = new Ed25519CmsXmlEncryptor(store).Encrypt(new XElement("secret", "value"));
        var wrongVersion = new XElement(encrypted.EncryptedElement);
        wrongVersion.SetAttributeValue("version", 3);

        var wrongNamespace = new XElement(
            XNamespace.Get(LegacyNamespace) + "encryptedSecret",
            new XAttribute("version", 2),
            encrypted.EncryptedElement.Value
        );
        var notBase64 = new XElement(
            XNamespace.Get(CmsNamespace) + "encryptedSecret",
            new XAttribute("version", 2),
            "not base64"
        );

        decryptor.Invoking(d => d.Decrypt(wrongVersion)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(wrongNamespace)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(notBase64)).Should().Throw<CryptographicException>();
    }

    [Fact]
    public void LoadOrCreateWithIncompleteVolumeThrows()
    {
        var missingKey = Path.Join(directory, "missing-key");
        Directory.CreateDirectory(missingKey);
        File.WriteAllBytes(Path.Join(missingKey, CertificateFileName), [0x30]);

        var missingCertificate = Path.Join(directory, "missing-certificate");
        Directory.CreateDirectory(missingCertificate);
        File.WriteAllBytes(Path.Join(missingCertificate, PrivateKeyFileName), [0x30]);

        FluentActions.Invoking(() => Load(missingKey)).Should().Throw<InvalidOperationException>();
        FluentActions
            .Invoking(() => Load(missingCertificate))
            .Should()
            .Throw<InvalidOperationException>();
    }

    [Fact]
    public void LegacyEncryptedElementStillDecrypts()
    {
        var volume = RestoreLegacyVolume();
        var store = Load(volume);
        var encryptedElement = XElement.Parse(ReadFixture("encrypted-secret.xml"));

        var decrypted = new Ed25519XmlDecryptor(Services(store)).Decrypt(encryptedElement);

        decrypted.ToString(SaveOptions.DisableFormatting).Should().Be(ReadFixture("plaintext.xml"));
    }

    [Fact]
    public void LegacyDecryptorKeepsItsRecordedTypeName()
    {
        typeof(Ed25519XmlDecryptor)
            .AssemblyQualifiedName.Should()
            .Be(ReadFixture("decryptor-type.txt"));
    }

    [Fact]
    public void LegacyKeyRingStillUnprotectsExistingPayloads()
    {
        var volume = RestoreLegacyVolume();
        var keysDirectory = new DirectoryInfo(Path.Join(volume, "keys"));
        var store = Load(volume);

        using var services = BuildDataProtectionProvider(store, keysDirectory);
        var plaintext = services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("fixture")
            .Unprotect(ReadFixture("protected-payload.txt"));

        plaintext.Should().Be("v1-protected-payload");
    }

    [Fact]
    public void LegacyPrivateKeyIsMigratedOnLoadAndLoadsAgain()
    {
        var volume = RestoreLegacyVolume();
        var privateKeyPath = Path.Join(volume, PrivateKeyFileName);
        var logger = new RecordingLogger<Ed25519CertificateStore>();

        var store = Ed25519CertificateStore.LoadOrCreate(
            new DirectoryInfo(volume),
            Password,
            logger
        );

        File.ReadAllBytes(privateKeyPath)[0].Should().Be(0x30);
        EncryptedPrivateKeyInfo
            .GetInstance(Asn1Object.FromByteArray(File.ReadAllBytes(privateKeyPath)))
            .EncryptionAlgorithm.Algorithm.Should()
            .Be(PkcsObjectIdentifiers.IdPbeS2);
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Information);

        var reloaded = Load(volume);
        reloaded.Certificate.GetEncoded().Should().Equal(store.Certificate.GetEncoded());

        var encryptedElement = XElement.Parse(ReadFixture("encrypted-secret.xml"));
        new Ed25519XmlDecryptor(Services(reloaded))
            .Decrypt(encryptedElement)
            .ToString(SaveOptions.DisableFormatting)
            .Should()
            .Be(ReadFixture("plaintext.xml"));
    }

    [Fact]
    public void LegacyPrivateKeyMigrationFailureKeepsTheOldFile()
    {
        var volume = RestoreLegacyVolume();
        var privateKeyPath = Path.Join(volume, PrivateKeyFileName);
        var original = File.ReadAllBytes(privateKeyPath);
        Directory.CreateDirectory(privateKeyPath + ".migrating");
        var logger = new RecordingLogger<Ed25519CertificateStore>();

        var store = Ed25519CertificateStore.LoadOrCreate(
            new DirectoryInfo(volume),
            Password,
            logger
        );

        File.ReadAllBytes(privateKeyPath).Should().Equal(original);
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Error);
        new Ed25519XmlDecryptor(Services(store))
            .Decrypt(XElement.Parse(ReadFixture("encrypted-secret.xml")))
            .ToString(SaveOptions.DisableFormatting)
            .Should()
            .Be(ReadFixture("plaintext.xml"));
    }

    [Fact]
    public void MigratePrivateKeyFailureLeavesNoTemporaryFileBehind()
    {
        var keyPath = Path.Join(directory, PrivateKeyFileName);
        Directory.CreateDirectory(keyPath);
        var logger = new RecordingLogger<Ed25519CertificateStore>();

        Ed25519CertificateStore.MigratePrivateKey(
            keyPath,
            new Ed25519PrivateKeyParameters(new SecureRandom()),
            Password,
            logger
        );

        File.Exists(keyPath + ".migrating").Should().BeFalse();
        Directory.Exists(keyPath).Should().BeTrue("the key that could not be replaced is kept");
        logger.LevelEntries.Should().ContainSingle(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public void DataProtectionProviderPersistsAndReloadsCmsProtectedKeyRing()
    {
        var certificateDirectory = Path.Join(directory, "certificate");
        var keysDirectory = new DirectoryInfo(Path.Join(directory, "keys"));
        var store = Load(certificateDirectory);
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
        persistedXml.Should().Contain(CmsNamespace);
        persistedXml.Should().Contain(typeof(Ed25519CmsXmlDecryptor).FullName);
        persistedXml.Should().NotContain("sensitive-value");

        using var reloadedServices = BuildDataProtectionProvider(
            Load(certificateDirectory),
            keysDirectory
        );
        var plaintext = reloadedServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("test")
            .Unprotect(protectedValue);

        plaintext.Should().Be("sensitive-value");
    }

    [Fact]
    public void ProductionWiringRegistersCmsEncryptorAndResolvesBothDecryptors()
    {
        var keysDirectory = new DirectoryInfo(directory);
        var services = new ServiceCollection();
        var dataProtection = services.AddDataProtection().SetApplicationName("CodigoActivo");

        var store = ApiHostConfiguration.ProtectKeysWithEd25519Certificate(
            services,
            dataProtection,
            keysDirectory,
            Password
        );

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<Ed25519CertificateStore>().Should().BeSameAs(store);
        provider
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value.XmlEncryptor.Should()
            .BeOfType<Ed25519CmsXmlEncryptor>();

        var plaintext = new XElement("secret", "value");
        var encrypted = provider
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value.XmlEncryptor!.Encrypt(plaintext);
        var decryptor = (IXmlDecryptor)
            ActivatorUtilities.CreateInstance(provider, encrypted.DecryptorType);

        XNode
            .DeepEquals(decryptor.Decrypt(encrypted.EncryptedElement), plaintext)
            .Should()
            .BeTrue();
        ActivatorUtilities
            .CreateInstance<Ed25519XmlDecryptor>(provider)
            .Should()
            .BeOfType<Ed25519XmlDecryptor>();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Ed25519CertificateStore Load(string path, string password = Password)
    {
        return Ed25519CertificateStore.LoadOrCreate(
            new DirectoryInfo(path),
            password,
            NullLogger<Ed25519CertificateStore>.Instance
        );
    }

    private static Ed25519CmsXmlDecryptor Decryptor(Ed25519CertificateStore store)
    {
        return new Ed25519CmsXmlDecryptor(Services(store));
    }

    private static ServiceProvider Services(Ed25519CertificateStore store)
    {
        return new ServiceCollection().AddSingleton(store).BuildServiceProvider();
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
            options.XmlEncryptor = new Ed25519CmsXmlEncryptor(store)
        );
        return services.BuildServiceProvider();
    }

    private static string ReadFixture(string name)
    {
        return File.ReadAllText(Path.Join(FixtureDirectory, name));
    }

    private string RestoreLegacyVolume()
    {
        var volume = Path.Join(directory, "legacy");
        Directory.CreateDirectory(Path.Join(volume, "keys"));
        File.Copy(
            Path.Join(FixtureDirectory, "certificate.cer"),
            Path.Join(volume, CertificateFileName)
        );
        File.Copy(
            Path.Join(FixtureDirectory, "private-key.v1.json"),
            Path.Join(volume, PrivateKeyFileName)
        );
        foreach (var key in Directory.GetFiles(Path.Join(FixtureDirectory, "keys")))
        {
            File.Copy(key, Path.Join(volume, "keys", Path.GetFileName(key)));
        }

        return volume;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Join(target, Path.GetFileName(file)));
        }
    }

    private static void RewritePrivateKeyPassword(string path)
    {
        var key = PrivateKeyFactory.DecryptKey(Password.ToCharArray(), File.ReadAllBytes(path));
        var random = new SecureRandom();
        File.WriteAllBytes(
            path,
            EncryptedPrivateKeyInfoFactory
                .CreateEncryptedPrivateKeyInfo(
                    NistObjectIdentifiers.IdAes256Cbc,
                    PkcsObjectIdentifiers.IdHmacWithSha512,
                    OtherPassword.ToCharArray(),
                    SecureRandom.GetNextBytes(random, 32),
                    600_000,
                    random,
                    key
                )
                .GetEncoded(Asn1Encodable.Der)
        );
    }

    private static byte[] ReadSignedContent(CmsSignedData signedData)
    {
        using var buffer = new MemoryStream();
        signedData.SignedContent.Write(buffer);
        return buffer.ToArray();
    }

    private static byte[] SignWithNewKey(byte[] content)
    {
        var random = new SecureRandom();
        var privateKey = new Ed25519PrivateKeyParameters(random);
        var name = new X509Name("CN=CodigoActivo Data Protection");
        var generator = new X509V3CertificateGenerator();
        generator.SetSerialNumber(new BigInteger(1, SecureRandom.GetNextBytes(random, 20)));
        generator.SetIssuerDN(name);
        generator.SetSubjectDN(name);
        generator.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        generator.SetNotAfter(DateTime.UtcNow.AddYears(20));
        generator.SetPublicKey(privateKey.GeneratePublicKey());
        var certificate = generator.Generate(
            new Asn1SignatureFactory("Ed25519", privateKey, random)
        );

        var signedGenerator = new CmsSignedDataGenerator(random) { UseDefiniteLength = true };
        signedGenerator.AddCertificate(certificate);
        signedGenerator.AddSignerInfoGenerator(
            new SignerInfoGeneratorBuilder().Build(
                new Asn1SignatureFactory("Ed25519", privateKey, random),
                certificate
            )
        );
        return signedGenerator
            .Generate(new CmsProcessableByteArray(content), encapsulate: true)
            .GetEncoded(Asn1Encodable.Der);
    }
}
