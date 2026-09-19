using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.API.Security;
using CodigoActivo.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Asn1.EdEC;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class Ed25519DataProtectionTests : IDisposable
{
    private const string Password = "a-strong-ed25519-certificate-password";
    private const string OtherPassword = "a-different-strong-certificate-password";
    private const string CertificateFileName = "key-encryption-ed25519.cer";
    private const string PrivateKeyFileName = "key-encryption-ed25519.key";
    private const string ElementNamespace = "urn:codigoactivo:data-protection:aes-gcm:v1";
    private const int ElementVersion = 1;

    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int SignatureSize = 64;
    private const int PrivateKeySize = 32;
    private const int SaltOffset = 1;
    private const int NonceOffset = SaltOffset + SaltSize;
    private const int TagOffset = NonceOffset + NonceSize;
    private const int CiphertextOffset = TagOffset + TagSize;
    private const int PrivateKeyFileSize = CiphertextOffset + PrivateKeySize;

    private static readonly XNamespace Namespace = XNamespace.Get(ElementNamespace);

    private readonly string directory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-ed25519-tests",
        Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void LoadOrCreateWritesTheVersionedAesGcmPrivateKeyContainer()
    {
        var store = Load(directory);
        var reloaded = Load(directory);

        store.Certificate.SigAlgOid.Should().Be(EdECObjectIdentifiers.id_Ed25519.Id);
        reloaded.Certificate.GetEncoded().Should().Equal(store.Certificate.GetEncoded());
        Directory.GetFiles(directory).Should().HaveCount(2);

        var contents = File.ReadAllBytes(Path.Join(directory, PrivateKeyFileName));
        contents.Should().HaveCount(PrivateKeyFileSize);
        contents[0].Should().Be(ElementVersion);
        contents.AsSpan(SaltOffset, SaltSize).ToArray().Should().NotEqual(new byte[SaltSize]);
        contents.AsSpan(NonceOffset, NonceSize).ToArray().Should().NotEqual(new byte[NonceSize]);
        contents
            .AsSpan(CiphertextOffset, PrivateKeySize)
            .ToArray()
            .Should()
            .NotEqual(store.PrivateKey.GetEncoded());
    }

    [Fact]
    public void LoadOrCreateUsesAFreshSaltAndNonceForEveryContainer()
    {
        Load(Path.Join(directory, "first"));
        Load(Path.Join(directory, "second"));

        var first = File.ReadAllBytes(Path.Join(directory, "first", PrivateKeyFileName));
        var second = File.ReadAllBytes(Path.Join(directory, "second", PrivateKeyFileName));

        first
            .AsSpan(SaltOffset, SaltSize)
            .ToArray()
            .Should()
            .NotEqual(second.AsSpan(SaltOffset, SaltSize).ToArray());
        first
            .AsSpan(NonceOffset, NonceSize)
            .ToArray()
            .Should()
            .NotEqual(second.AsSpan(NonceOffset, NonceSize).ToArray());
    }

    [Fact]
    public void LoadWithWrongPasswordFailsClosed()
    {
        Load(directory);

        var act = () => Load(directory, OtherPassword);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void LoadWithSwappedCertificateFailsClosed()
    {
        var store = Load(Path.Join(directory, "first"));
        Load(Path.Join(directory, "second"));
        var swapped = Path.Join(directory, "swapped");
        CopyVolume(Path.Join(directory, "first"), swapped);
        File.Copy(
            Path.Join(directory, "second", CertificateFileName),
            Path.Join(swapped, CertificateFileName),
            overwrite: true
        );

        var act = () => Load(swapped);

        act.Should().Throw<CryptographicException>();
        store.Certificate.Should().NotBeNull();
    }

    [Fact]
    public void LoadWithForeignPrivateKeyFailsClosed()
    {
        var store = Load(directory);
        var foreign = Path.Join(directory, "foreign");
        CopyVolume(directory, foreign);
        WritePrivateKeyFile(
            Path.Join(foreign, PrivateKeyFileName),
            new Ed25519PrivateKeyParameters(new SecureRandom()),
            Password,
            store.Certificate.GetEncoded()
        );

        var act = () => Load(foreign);

        act.Should()
            .Throw<CryptographicException>()
            .WithMessage("*certificate and private key do not match*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PrivateKeyFileSize - 1)]
    [InlineData(PrivateKeyFileSize + 1)]
    public void LoadWithPrivateKeyFileOfTheWrongLengthFailsClosed(int length)
    {
        Load(directory);
        var broken = Path.Join(directory, "broken");
        CopyVolume(directory, broken);
        var path = Path.Join(broken, PrivateKeyFileName);
        var contents = File.ReadAllBytes(path);
        Array.Resize(ref contents, length);
        File.WriteAllBytes(path, contents);

        var act = () => Load(broken);

        act.Should()
            .Throw<CryptographicException>()
            .WithMessage("*private key file is invalid*")
            .And.Message.Should()
            .Contain("Recreate the api-dataprotection volume");
    }

    [Fact]
    public void LoadWithUnknownPrivateKeyVersionFailsClosed()
    {
        Load(directory);
        var broken = Path.Join(directory, "broken");
        CopyVolume(directory, broken);
        var path = Path.Join(broken, PrivateKeyFileName);
        var contents = File.ReadAllBytes(path);
        contents[0] = 2;
        File.WriteAllBytes(path, contents);

        var act = () => Load(broken);

        act.Should()
            .Throw<CryptographicException>()
            .WithMessage("*private key file is invalid*")
            .And.Message.Should()
            .Contain("Recreate the api-dataprotection volume");
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
    public void LoadWithNonEd25519CertificateThrows()
    {
        var volume = Path.Join(directory, "ecdsa");
        Directory.CreateDirectory(volume);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Not Ed25519", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(1)
        );
        File.WriteAllBytes(Path.Join(volume, CertificateFileName), certificate.RawData);
        File.WriteAllBytes(Path.Join(volume, PrivateKeyFileName), new byte[PrivateKeyFileSize]);

        var act = () => Load(volume);

        act.Should().Throw<InvalidOperationException>().WithMessage("*must use Ed25519*");
    }

    [Fact]
    public void EncryptProducesTheSignedAesGcmElementThatRoundTrips()
    {
        var store = Load(directory);
        var plaintext = new XElement("secret", new XAttribute("id", "key-1"), "value");

        var encrypted = new Ed25519AesGcmXmlEncryptor(store).Encrypt(plaintext);
        var element = encrypted.EncryptedElement;
        var decrypted = Decryptor(store).Decrypt(element);

        XNode.DeepEquals(decrypted, plaintext).Should().BeTrue();
        encrypted.DecryptorType.Should().Be<Ed25519AesGcmXmlDecryptor>();
        element.Name.Should().Be(Namespace + "encryptedSecret");
        ((string?)element.Attribute("version")).Should().Be("1");
        element.ToString().Should().NotContain("value");

        Field(element, "salt").Should().HaveCount(SaltSize);
        Field(element, "nonce").Should().HaveCount(NonceSize);
        Field(element, "tag").Should().HaveCount(TagSize);
        Field(element, "signature").Should().HaveCount(SignatureSize);
        Field(element, "ciphertext")
            .Should()
            .HaveCount(
                plaintext.ToString(SaveOptions.DisableFormatting).Length,
                "AES-GCM ciphertext is as long as its plaintext"
            );

        VerifySignature(store, SignedPayloadOf(element), Field(element, "signature"))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void EncryptUsesAFreshSaltAndNonceForEveryElement()
    {
        var store = Load(directory);
        var encryptor = new Ed25519AesGcmXmlEncryptor(store);

        var first = encryptor.Encrypt(new XElement("secret", "value")).EncryptedElement;
        var second = encryptor.Encrypt(new XElement("secret", "value")).EncryptedElement;

        Field(first, "salt").Should().NotEqual(Field(second, "salt"));
        Field(first, "nonce").Should().NotEqual(Field(second, "nonce"));
        Field(first, "ciphertext").Should().NotEqual(Field(second, "ciphertext"));
    }

    [Fact]
    public void DecryptWithSwappedCertificateFailsClosed()
    {
        var store = Load(Path.Join(directory, "first"));
        var other = Load(Path.Join(directory, "second"));
        var encrypted = new Ed25519AesGcmXmlEncryptor(store).Encrypt(
            new XElement("secret", "value")
        );

        var act = () => Decryptor(other).Decrypt(encrypted.EncryptedElement);

        act.Should().Throw<CryptographicException>().WithMessage("*invalid Ed25519 signature*");
    }

    [Fact]
    public void DecryptWithWrongCertificatePasswordFailsClosed()
    {
        var store = Load(directory);
        var encrypted = new Ed25519AesGcmXmlEncryptor(store).Encrypt(
            new XElement("secret", "value")
        );
        var repassworded = Path.Join(directory, "repassworded");
        CopyVolume(directory, repassworded);
        WritePrivateKeyFile(
            Path.Join(repassworded, PrivateKeyFileName),
            store.PrivateKey,
            OtherPassword,
            store.Certificate.GetEncoded()
        );

        var act = () =>
            Decryptor(Load(repassworded, OtherPassword)).Decrypt(encrypted.EncryptedElement);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void DecryptVerifiesTheSignatureBeforeDerivingTheKey()
    {
        var store = Load(directory);
        var encrypted = new Ed25519AesGcmXmlEncryptor(store)
            .Encrypt(new XElement("secret", "value"))
            .EncryptedElement;
        var otherSalt = Argon2idKeyDerivation.CreateSalt();

        var unsigned = Rebuild(encrypted, salt: otherSalt, signature: null);
        var resigned = Rebuild(encrypted, salt: otherSalt, signature: null);
        resigned.Element(Namespace + "signature")!.Value = Convert.ToBase64String(
            Sign(store, SignedPayloadOf(resigned))
        );

        Decryptor(store)
            .Invoking(d => d.Decrypt(unsigned))
            .Should()
            .Throw<CryptographicException>()
            .WithMessage("*invalid Ed25519 signature*");
        Decryptor(store)
            .Invoking(d => d.Decrypt(resigned))
            .Should()
            .Throw<AuthenticationTagMismatchException>(
                "a verified signature is the only way to reach the Argon2id derivation"
            );
    }

    [Fact]
    public void DecryptRejectsEveryBitFlipInEveryField()
    {
        var store = Load(directory);
        var decryptor = Decryptor(store);
        var encrypted = new Ed25519AesGcmXmlEncryptor(store)
            .Encrypt(new XElement("secret", "value"))
            .EncryptedElement;
        var accepted = new List<string>();

        foreach (var name in new[] { "salt", "nonce", "tag", "ciphertext", "signature" })
        {
            var original = Field(encrypted, name);
            for (var offset = 0; offset < original.Length; offset++)
            {
                var tampered = (byte[])original.Clone();
                tampered[offset] ^= 0x01;
                var element = new XElement(encrypted);
                element.Element(Namespace + name)!.Value = Convert.ToBase64String(tampered);

                try
                {
                    decryptor.Decrypt(element);
                    accepted.Add($"{name}[{offset}]");
                }
                catch (CryptographicException)
                {
                    continue;
                }
            }
        }

        accepted.Should().BeEmpty();
    }

    [Theory]
    [InlineData("salt")]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("signature")]
    public void DecryptRejectsFieldsOfTheWrongLength(string name)
    {
        var store = Load(directory);
        var decryptor = Decryptor(store);
        var encrypted = new Ed25519AesGcmXmlEncryptor(store)
            .Encrypt(new XElement("secret", "value"))
            .EncryptedElement;

        var shortened = new XElement(encrypted);
        shortened.Element(Namespace + name)!.Value = Convert.ToBase64String(
            Field(encrypted, name).AsSpan(1).ToArray()
        );
        var missing = new XElement(encrypted);
        missing.Element(Namespace + name)!.Remove();
        var notBase64 = new XElement(encrypted);
        notBase64.Element(Namespace + name)!.Value = "not base64";

        decryptor.Invoking(d => d.Decrypt(shortened)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(missing)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(notBase64)).Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptRejectsCiphertextOutsideTheAcceptedSize()
    {
        var store = Load(directory);
        var decryptor = Decryptor(store);
        var empty = BuildElement(ciphertext: []);
        var oversized = BuildElement(ciphertext: new byte[(1024 * 1024) + 1]);

        decryptor.Invoking(d => d.Decrypt(empty)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(oversized)).Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptRejectsForeignNamespaceAndVersion()
    {
        var store = Load(directory);
        var decryptor = Decryptor(store);
        var encrypted = new Ed25519AesGcmXmlEncryptor(store)
            .Encrypt(new XElement("secret", "value"))
            .EncryptedElement;

        var wrongVersion = new XElement(encrypted);
        wrongVersion.SetAttributeValue("version", 2);
        var missingVersion = new XElement(encrypted);
        missingVersion.Attribute("version")!.Remove();
        var wrongNamespace = new XElement(
            XNamespace.Get("urn:codigoactivo:data-protection:ed25519:v1") + "encryptedSecret",
            new XAttribute("version", ElementVersion),
            encrypted.Elements().Select(child => new XElement(child))
        );
        var wrongName = new XElement(encrypted);
        wrongName.Name = Namespace + "encryptedKey";

        decryptor.Invoking(d => d.Decrypt(wrongVersion)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(missingVersion)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(wrongNamespace)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(wrongName)).Should().Throw<CryptographicException>();
        FluentActions
            .Invoking(() => decryptor.Decrypt(null!))
            .Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void DecryptRejectsPlaintextThatIsNotXml()
    {
        var store = Load(directory);
        var element = ProtectBytes(store, "not xml at all"u8.ToArray());

        var act = () => Decryptor(store).Decrypt(element);

        act.Should()
            .Throw<CryptographicException>()
            .WithMessage("*decrypted Data Protection key is invalid*");
    }

    [Fact]
    public void DataProtectionProviderPersistsAndReloadsTheProtectedKeyRing()
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
        persistedXml.Should().Contain(ElementNamespace);
        persistedXml.Should().Contain(typeof(Ed25519AesGcmXmlDecryptor).FullName);
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
    public void ProductionWiringRegistersTheAesGcmEncryptorAndResolvesItsDecryptor()
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
        var encryptor = provider
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value.XmlEncryptor.Should()
            .BeOfType<Ed25519AesGcmXmlEncryptor>()
            .Subject;

        var plaintext = new XElement("secret", "value");
        var encrypted = encryptor.Encrypt(plaintext);
        var decryptor = (IXmlDecryptor)
            ActivatorUtilities.CreateInstance(provider, encrypted.DecryptorType);

        decryptor.Should().BeOfType<Ed25519AesGcmXmlDecryptor>();
        XNode
            .DeepEquals(decryptor.Decrypt(encrypted.EncryptedElement), plaintext)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ProductionWiringProtectsPayloadsWithAesGcmOverTheEd25519KeyRing()
    {
        var keysDirectory = new DirectoryInfo(Path.Join(directory, "keys"));
        string protectedValue;

        using (var provider = BuildProductionLikeProvider(keysDirectory))
        {
            protectedValue = provider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("session")
                .Protect("session-payload");
        }

        var keyFile = keysDirectory.GetFiles("key-*.xml").Should().ContainSingle().Subject;
        var persistedXml = File.ReadAllText(keyFile.FullName);
        persistedXml.Should().Contain(ElementNamespace);
        persistedXml.Should().Contain(typeof(Ed25519AesGcmXmlDecryptor).FullName);
        persistedXml.Should().Contain("AES_256_GCM");

        using var reloaded = BuildProductionLikeProvider(keysDirectory);
        reloaded
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("session")
            .Unprotect(protectedValue)
            .Should()
            .Be("session-payload");
    }

    [Fact]
    public void ProtectPayloadsWithAesGcmConfiguresAes256GcmForEveryEnvironment()
    {
        var services = new ServiceCollection();
        var dataProtection = services.AddDataProtection().SetApplicationName("CodigoActivo");

        ApiHostConfiguration.ProtectPayloadsWithAesGcm(dataProtection);

        using var provider = services.BuildServiceProvider();
        var configuration = provider
            .GetRequiredService<IOptions<KeyManagementOptions>>()
            .Value.AuthenticatedEncryptorConfiguration.Should()
            .BeOfType<AuthenticatedEncryptorConfiguration>()
            .Subject;
        configuration.EncryptionAlgorithm.Should().Be(EncryptionAlgorithm.AES_256_GCM);
        configuration.ValidationAlgorithm.Should().Be(ValidationAlgorithm.HMACSHA512);

        var protector = provider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("payload");
        protector.Unprotect(protector.Protect("payload-value")).Should().Be("payload-value");
        FluentActions
            .Invoking(() => ApiHostConfiguration.ProtectPayloadsWithAesGcm(null!))
            .Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApiAssemblyNoLongerContainsTheRetiredProtectionTypes()
    {
        var types = typeof(Ed25519CertificateStore)
            .Assembly.GetTypes()
            .Select(type => type.Name)
            .ToList();

        types
            .Should()
            .NotContain([
                "LegacyEd25519Protection",
                "Ed25519XmlDecryptor",
                "CmsKeyProtection",
                "Ed25519CmsXmlEncryptor",
                "Ed25519CmsXmlDecryptor",
            ]);
        types.Should().Contain(nameof(Ed25519AesGcmXmlEncryptor));
        types.Should().Contain(nameof(Ed25519AesGcmXmlDecryptor));
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
        return Ed25519CertificateStore.LoadOrCreate(new DirectoryInfo(path), password);
    }

    private static Ed25519AesGcmXmlDecryptor Decryptor(Ed25519CertificateStore store)
    {
        return new Ed25519AesGcmXmlDecryptor(
            new ServiceCollection().AddSingleton(store).BuildServiceProvider()
        );
    }

    private static ServiceProvider BuildProductionLikeProvider(DirectoryInfo keysDirectory)
    {
        var services = new ServiceCollection();
        var dataProtection = services.AddDataProtection().SetApplicationName("CodigoActivo");

        ApiHostConfiguration.ProtectPayloadsWithAesGcm(dataProtection);
        ApiHostConfiguration.ProtectKeysWithEd25519Certificate(
            services,
            dataProtection,
            keysDirectory,
            Password
        );

        return services.BuildServiceProvider();
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
            options.XmlEncryptor = new Ed25519AesGcmXmlEncryptor(store)
        );
        return services.BuildServiceProvider();
    }

    private static byte[] Field(XElement element, string name)
    {
        return Convert.FromBase64String(element.Element(Namespace + name)!.Value);
    }

    private static XElement Rebuild(XElement element, byte[] salt, byte[]? signature)
    {
        return BuildElement(
            salt: salt,
            nonce: Field(element, "nonce"),
            tag: Field(element, "tag"),
            ciphertext: Field(element, "ciphertext"),
            signature: signature ?? Field(element, "signature")
        );
    }

    private static XElement BuildElement(
        byte[]? salt = null,
        byte[]? nonce = null,
        byte[]? tag = null,
        byte[]? ciphertext = null,
        byte[]? signature = null
    )
    {
        return new XElement(
            Namespace + "encryptedSecret",
            new XAttribute("version", ElementVersion),
            new XElement(Namespace + "salt", Convert.ToBase64String(salt ?? new byte[SaltSize])),
            new XElement(Namespace + "nonce", Convert.ToBase64String(nonce ?? new byte[NonceSize])),
            new XElement(Namespace + "tag", Convert.ToBase64String(tag ?? new byte[TagSize])),
            new XElement(
                Namespace + "ciphertext",
                Convert.ToBase64String(ciphertext ?? new byte[32])
            ),
            new XElement(
                Namespace + "signature",
                Convert.ToBase64String(signature ?? new byte[SignatureSize])
            )
        );
    }

    private static XElement ProtectBytes(Ed25519CertificateStore store, byte[] plaintext)
    {
        var salt = Argon2idKeyDerivation.CreateSalt();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];
        using var cipher = new AesGcm(Argon2idKeyDerivation.DeriveKey(Password, salt), TagSize);
        cipher.Encrypt(
            nonce,
            plaintext,
            ciphertext,
            tag,
            "CodigoActivo Data Protection key element v1"u8.ToArray()
        );

        var element = BuildElement(salt, nonce, tag, ciphertext);
        element.Element(Namespace + "signature")!.Value = Convert.ToBase64String(
            Sign(store, SignedPayloadOf(element))
        );
        return element;
    }

    private static void WritePrivateKeyFile(
        string path,
        Ed25519PrivateKeyParameters privateKey,
        string password,
        byte[] certificateBytes
    )
    {
        var salt = Argon2idKeyDerivation.CreateSalt();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var contents = new byte[PrivateKeyFileSize];
        contents[0] = ElementVersion;
        salt.CopyTo(contents, SaltOffset);
        nonce.CopyTo(contents, NonceOffset);

        using var cipher = new AesGcm(Argon2idKeyDerivation.DeriveKey(password, salt), TagSize);
        cipher.Encrypt(
            nonce,
            privateKey.GetEncoded(),
            contents.AsSpan(CiphertextOffset),
            contents.AsSpan(TagOffset, TagSize),
            certificateBytes
        );
        File.WriteAllBytes(path, contents);
    }

    private static byte[] SignedPayloadOf(XElement element)
    {
        return SignedPayload(
            ElementVersion,
            Field(element, "salt"),
            Field(element, "nonce"),
            Field(element, "tag"),
            Field(element, "ciphertext")
        );
    }

    private static byte[] SignedPayload(int version, params byte[][] fields)
    {
        using var buffer = new MemoryStream();
        Span<byte> prefix = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(prefix, version);
        buffer.Write(prefix);

        foreach (var field in fields)
        {
            BinaryPrimitives.WriteInt32BigEndian(prefix, field.Length);
            buffer.Write(prefix);
            buffer.Write(field);
        }

        return buffer.ToArray();
    }

    private static byte[] Sign(Ed25519CertificateStore store, byte[] payload)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, store.PrivateKey);
        signer.BlockUpdate(payload, 0, payload.Length);
        return signer.GenerateSignature();
    }

    private static bool VerifySignature(
        Ed25519CertificateStore store,
        byte[] payload,
        byte[] signature
    )
    {
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, store.Certificate.GetPublicKey());
        verifier.BlockUpdate(payload, 0, payload.Length);
        return verifier.VerifySignature(signature);
    }

    private static void CopyVolume(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Join(target, Path.GetFileName(file)), overwrite: true);
        }
    }
}
