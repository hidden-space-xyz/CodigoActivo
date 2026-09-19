using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using CodigoActivo.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1.EdEC;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace CodigoActivo.API.Security;

/// <summary>
/// Loads and maintains the persisted Ed25519 certificate material. The private key file is a fixed-size
/// binary container: a one-byte version, the Argon2id salt, the AES-256-GCM nonce, the authentication tag
/// and the encrypted 32-byte Ed25519 seed. The key encrypting it is Argon2id over the certificate
/// password, and the DER bytes of the certificate are the associated data, so a key file only opens
/// next to the certificate it belongs to. The store also carries the certificate password because the
/// same derivation protects the Data Protection key ring elements.
/// </summary>
public sealed class Ed25519CertificateStore
{
    internal const byte FormatVersion = 1;
    internal const int NonceSize = 12;
    internal const int TagSize = 16;

    internal const int SaltOffset = 1;
    internal const int NonceOffset = SaltOffset + Argon2idKeyDerivation.SaltSize;
    internal const int TagOffset = NonceOffset + NonceSize;
    internal const int CiphertextOffset = TagOffset + TagSize;

    private const string CertificateFileName = "key-encryption-ed25519.cer";
    private const string PrivateKeyFileName = "key-encryption-ed25519.key";

    private static readonly int PrivateKeyFileSize =
        CiphertextOffset + Ed25519PrivateKeyParameters.KeySize;

    private readonly Ed25519PrivateKeyParameters privateKey;

    private Ed25519CertificateStore(
        X509Certificate certificate,
        Ed25519PrivateKeyParameters privateKey,
        string password
    )
    {
        Certificate = certificate;
        this.privateKey = privateKey;
        CertificatePassword = password;
    }

    /// <summary>
    /// Gets the certificate used to sign the protected data-protection keys.
    /// </summary>
    public X509Certificate Certificate { get; }

    internal Ed25519PrivateKeyParameters PrivateKey => privateKey;

    internal string CertificatePassword { get; }

    /// <summary>
    /// Loads the signing certificate or creates and persists a new one.
    /// </summary>
    /// <param name="directory">Directory where the certificate material is stored.</param>
    /// <param name="password">Plain-text password protecting the private key and the key ring.</param>
    /// <returns>The certificate store containing the certificate and private key.</returns>
    public static Ed25519CertificateStore LoadOrCreate(DirectoryInfo directory, string password)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        directory.Create();
        var certificatePath = Path.Join(directory.FullName, CertificateFileName);
        var privateKeyPath = Path.Join(directory.FullName, PrivateKeyFileName);
        var certificateExists = File.Exists(certificatePath);
        var privateKeyExists = File.Exists(privateKeyPath);

        if (certificateExists != privateKeyExists)
        {
            throw new InvalidOperationException(
                "The Ed25519 Data Protection certificate is incomplete. Recreate the api-dataprotection volume."
            );
        }

        if (!certificateExists)
        {
            Create(certificatePath, privateKeyPath, password);
        }

        return Load(certificatePath, privateKeyPath, password);
    }

    private static void Create(string certificatePath, string privateKeyPath, string password)
    {
        var random = new SecureRandom();
        var privateKey = new Ed25519PrivateKeyParameters(random);
        var publicKey = privateKey.GeneratePublicKey();
        var name = new X509Name("CN=CodigoActivo Data Protection");
        var generator = new X509V3CertificateGenerator();

        generator.SetSerialNumber(new BigInteger(1, SecureRandom.GetNextBytes(random, 20)));
        generator.SetIssuerDN(name);
        generator.SetSubjectDN(name);
        generator.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        generator.SetNotAfter(DateTime.UtcNow.AddYears(20));
        generator.SetPublicKey(publicKey);
        generator.AddExtension(
            X509Extensions.BasicConstraints,
            critical: true,
            new BasicConstraints(false)
        );
        generator.AddExtension(
            X509Extensions.KeyUsage,
            critical: true,
            new KeyUsage(KeyUsage.DigitalSignature)
        );

        var certificate = generator.Generate(
            new Asn1SignatureFactory("Ed25519", privateKey, random)
        );
        certificate.Verify(publicKey);

        var certificateBytes = certificate.GetEncoded();
        WriteFile(certificatePath, certificateBytes, privateFile: false);
        WriteFile(
            privateKeyPath,
            EncryptPrivateKey(privateKey, password, certificateBytes),
            privateFile: true
        );
    }

    private static Ed25519CertificateStore Load(
        string certificatePath,
        string privateKeyPath,
        string password
    )
    {
        var certificateBytes = File.ReadAllBytes(certificatePath);
        var certificate = new X509CertificateParser().ReadCertificate(certificateBytes);
        if (
            !string.Equals(
                certificate.SigAlgOid,
                EdECObjectIdentifiers.id_Ed25519.Id,
                StringComparison.Ordinal
            ) || certificate.GetPublicKey() is not Ed25519PublicKeyParameters publicKey
        )
        {
            throw new InvalidOperationException(
                "The Data Protection certificate must use Ed25519."
            );
        }

        certificate.Verify(publicKey);

        var privateKey = DecryptPrivateKey(
            File.ReadAllBytes(privateKeyPath),
            password,
            certificateBytes
        );

        if (
            !CryptographicOperations.FixedTimeEquals(
                privateKey.GeneratePublicKey().GetEncoded(),
                publicKey.GetEncoded()
            )
        )
        {
            throw new CryptographicException(
                "The Ed25519 certificate and private key do not match."
            );
        }

        return new Ed25519CertificateStore(certificate, privateKey, password);
    }

    private static byte[] EncryptPrivateKey(
        Ed25519PrivateKeyParameters privateKey,
        string password,
        byte[] certificateBytes
    )
    {
        var contents = new byte[PrivateKeyFileSize];
        var salt = Argon2idKeyDerivation.CreateSalt();
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var encryptionKey = Argon2idKeyDerivation.DeriveKey(password, salt);
        var plaintext = privateKey.GetEncoded();

        try
        {
            contents[0] = FormatVersion;
            salt.CopyTo(contents, SaltOffset);
            nonce.CopyTo(contents, NonceOffset);

            using var cipher = new AesGcm(encryptionKey, TagSize);
            cipher.Encrypt(
                nonce,
                plaintext,
                contents.AsSpan(CiphertextOffset),
                contents.AsSpan(TagOffset, TagSize),
                certificateBytes
            );

            return contents;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static Ed25519PrivateKeyParameters DecryptPrivateKey(
        byte[] contents,
        string password,
        byte[] certificateBytes
    )
    {
        if (contents.Length != PrivateKeyFileSize || contents[0] != FormatVersion)
        {
            throw new CryptographicException(
                "The Ed25519 private key file is invalid. Recreate the api-dataprotection volume."
            );
        }

        var encryptionKey = Argon2idKeyDerivation.DeriveKey(
            password,
            contents.AsSpan(SaltOffset, Argon2idKeyDerivation.SaltSize)
        );
        var plaintext = new byte[Ed25519PrivateKeyParameters.KeySize];

        try
        {
            using var cipher = new AesGcm(encryptionKey, TagSize);
            cipher.Decrypt(
                contents.AsSpan(NonceOffset, NonceSize),
                contents.AsSpan(CiphertextOffset),
                contents.AsSpan(TagOffset, TagSize),
                plaintext,
                certificateBytes
            );

            return new Ed25519PrivateKeyParameters(plaintext);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "The Ed25519 private key file could not be decrypted.",
                ex
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void WriteFile(string path, byte[] contents, bool privateFile)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough
        );

        if (privateFile && OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        stream.Write(contents);
        stream.Flush(flushToDisk: true);
    }
}

/// <summary>
/// Protects data-protection key elements with AES-256-GCM under a key derived from the certificate
/// password with Argon2id, and signs the result with the Ed25519 certificate. The signature covers a
/// length-prefixed encoding of the version, salt, nonce, tag and ciphertext, and it is verified against
/// the certificate pinned in the key volume before any key is derived or any byte is decrypted.
/// </summary>
internal static class Ed25519KeyProtection
{
    internal const int ElementVersion = 1;
    internal const int MaximumCiphertextSize = 1024 * 1024;

    internal static readonly XNamespace Namespace = "urn:codigoactivo:data-protection:aes-gcm:v1";

    private const string ElementName = "encryptedSecret";
    private const string VersionAttributeName = "version";

    private static readonly byte[] AdditionalData = Encoding.UTF8.GetBytes(
        "CodigoActivo Data Protection key element v1"
    );

    internal static XElement Encrypt(XElement plaintextElement, Ed25519CertificateStore store)
    {
        var plaintext = Encoding.UTF8.GetBytes(
            plaintextElement.ToString(SaveOptions.DisableFormatting)
        );
        var salt = Argon2idKeyDerivation.CreateSalt();
        var nonce = RandomNumberGenerator.GetBytes(Ed25519CertificateStore.NonceSize);
        var tag = new byte[Ed25519CertificateStore.TagSize];
        var ciphertext = new byte[plaintext.Length];
        var encryptionKey = Argon2idKeyDerivation.DeriveKey(store.CertificatePassword, salt);

        try
        {
            using var cipher = new AesGcm(encryptionKey, Ed25519CertificateStore.TagSize);
            cipher.Encrypt(nonce, plaintext, ciphertext, tag, AdditionalData);

            var signature = Sign(
                store.PrivateKey,
                BuildSignedPayload(ElementVersion, salt, nonce, tag, ciphertext)
            );

            return new XElement(
                Namespace + ElementName,
                new XAttribute(VersionAttributeName, ElementVersion),
                new XElement(Namespace + "salt", Convert.ToBase64String(salt)),
                new XElement(Namespace + "nonce", Convert.ToBase64String(nonce)),
                new XElement(Namespace + "tag", Convert.ToBase64String(tag)),
                new XElement(Namespace + "ciphertext", Convert.ToBase64String(ciphertext)),
                new XElement(Namespace + "signature", Convert.ToBase64String(signature))
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    internal static XElement Decrypt(XElement encryptedElement, Ed25519CertificateStore store)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        if (
            encryptedElement.Name != Namespace + ElementName
            || !int.TryParse(
                (string?)encryptedElement.Attribute(VersionAttributeName),
                CultureInfo.InvariantCulture,
                out var version
            )
            || version != ElementVersion
        )
        {
            throw Invalid();
        }

        var salt = Decode(encryptedElement, "salt", Argon2idKeyDerivation.SaltSize);
        var nonce = Decode(encryptedElement, "nonce", Ed25519CertificateStore.NonceSize);
        var tag = Decode(encryptedElement, "tag", Ed25519CertificateStore.TagSize);
        var signature = Decode(
            encryptedElement,
            "signature",
            Ed25519PrivateKeyParameters.SignatureSize
        );
        var ciphertext = Decode(encryptedElement, "ciphertext", expectedSize: null);

        if (ciphertext.Length is 0 or > MaximumCiphertextSize)
        {
            throw Invalid();
        }

        Verify(
            store.Certificate,
            BuildSignedPayload(version, salt, nonce, tag, ciphertext),
            signature
        );

        var encryptionKey = Argon2idKeyDerivation.DeriveKey(store.CertificatePassword, salt);
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var cipher = new AesGcm(encryptionKey, Ed25519CertificateStore.TagSize);
            cipher.Decrypt(nonce, ciphertext, tag, plaintext, AdditionalData);

            return XElement.Parse(
                Encoding.UTF8.GetString(plaintext),
                LoadOptions.PreserveWhitespace
            );
        }
        catch (Exception ex) when (ex is not CryptographicException and not OutOfMemoryException)
        {
            throw new CryptographicException("The decrypted Data Protection key is invalid.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] Sign(Ed25519PrivateKeyParameters privateKey, byte[] payload)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privateKey);
        signer.BlockUpdate(payload, 0, payload.Length);
        return signer.GenerateSignature();
    }

    private static void Verify(X509Certificate certificate, byte[] payload, byte[] signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, certificate.GetPublicKey());
        verifier.BlockUpdate(payload, 0, payload.Length);

        if (!verifier.VerifySignature(signature))
        {
            throw new CryptographicException(
                "The encrypted Data Protection key has an invalid Ed25519 signature."
            );
        }
    }

    private static byte[] BuildSignedPayload(
        int version,
        byte[] salt,
        byte[] nonce,
        byte[] tag,
        byte[] ciphertext
    )
    {
        ReadOnlySpan<byte[]> fields = [salt, nonce, tag, ciphertext];
        var length = sizeof(int);
        foreach (var field in fields)
        {
            length += sizeof(int) + field.Length;
        }

        var payload = new byte[length];
        BinaryPrimitives.WriteInt32BigEndian(payload, version);
        var offset = sizeof(int);
        foreach (var field in fields)
        {
            BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset), field.Length);
            offset += sizeof(int);
            field.CopyTo(payload, offset);
            offset += field.Length;
        }

        return payload;
    }

    private static byte[] Decode(XElement parent, string name, int? expectedSize)
    {
        var element = parent.Element(Namespace + name);
        if (element is null)
        {
            throw Invalid();
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(element.Value);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.", ex);
        }

        if (expectedSize is null || decoded.Length == expectedSize)
        {
            return decoded;
        }

        CryptographicOperations.ZeroMemory(decoded);
        throw Invalid();
    }

    private static CryptographicException Invalid()
    {
        return new CryptographicException("The encrypted Data Protection key is invalid.");
    }
}

/// <summary>
/// Encrypts data-protection key elements into the signed AES-256-GCM element described by
/// <see cref="Ed25519KeyProtection"/>.
/// </summary>
/// <param name="certificateStore">Store that provides the certificate, signing key and password.</param>
public sealed class Ed25519AesGcmXmlEncryptor(Ed25519CertificateStore certificateStore)
    : IXmlEncryptor
{
    /// <summary>
    /// Encrypts and signs the XML element for data-protection storage.
    /// </summary>
    /// <param name="plaintextElement">Unencrypted XML element to protect.</param>
    /// <returns>The resulting encrypted xml info value.</returns>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);
        ArgumentNullException.ThrowIfNull(certificateStore);

        return new EncryptedXmlInfo(
            Ed25519KeyProtection.Encrypt(plaintextElement, certificateStore),
            typeof(Ed25519AesGcmXmlDecryptor)
        );
    }
}

/// <summary>
/// Verifies and decrypts the elements written by <see cref="Ed25519AesGcmXmlEncryptor"/>. Data
/// Protection resolves this type by the name recorded in each key file.
/// </summary>
public sealed class Ed25519AesGcmXmlDecryptor : IXmlDecryptor
{
    private readonly Ed25519CertificateStore certificateStore;

    /// <summary>
    /// Initializes the decryptor with its required dependencies.
    /// </summary>
    /// <param name="services">Service collection or provider used to resolve dependencies.</param>
    public Ed25519AesGcmXmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        certificateStore = services.GetRequiredService<Ed25519CertificateStore>();
    }

    /// <summary>
    /// Verifies the Ed25519 signature against the pinned certificate and decrypts the element.
    /// </summary>
    /// <param name="encryptedElement">Protected XML element to verify and decrypt.</param>
    /// <returns>The resulting x element value.</returns>
    public XElement Decrypt(XElement encryptedElement)
    {
        return Ed25519KeyProtection.Decrypt(encryptedElement, certificateStore);
    }
}
