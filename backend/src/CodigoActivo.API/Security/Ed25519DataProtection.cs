using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
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
using ContentInfo = Org.BouncyCastle.Asn1.Cms.ContentInfo;

namespace CodigoActivo.API.Security;

/// <summary>
/// Loads and maintains the persisted Ed25519 certificate material. The private key is stored as a
/// PKCS#8 <c>EncryptedPrivateKeyInfo</c> (PBES2: PBKDF2-HMAC-SHA-512 with AES-256-CBC) produced and
/// parsed by BouncyCastle. A key file still written in the retired v1 JSON envelope is read through
/// <see cref="Ed25519XmlDecryptor"/>'s legacy reader and rewritten in the standard format on load; a
/// rewrite that fails keeps the previous file and leaves no temporary file behind.
/// The store also carries the certificate password, because it derives the key-wrapping key of the
/// CMS container that protects the Data Protection key ring.
/// </summary>
public sealed partial class Ed25519CertificateStore
{
    internal const int Pbkdf2Iterations = 600_000;
    internal const int SaltSize = 32;

    private const string CertificateFileName = "key-encryption-ed25519.cer";
    private const string PrivateKeyFileName = "key-encryption-ed25519.key";
    private const string MigrationFileSuffix = ".migrating";

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
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <returns>The certificate store containing the certificate and private key.</returns>
    public static Ed25519CertificateStore LoadOrCreate(
        DirectoryInfo directory,
        string password,
        ILogger<Ed25519CertificateStore> logger
    )
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(logger);

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

        return Load(certificatePath, privateKeyPath, password, logger);
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

        WriteFile(
            certificatePath,
            certificate.GetEncoded(),
            FileMode.CreateNew,
            privateFile: false
        );
        WriteFile(
            privateKeyPath,
            EncryptPrivateKey(privateKey, password, random),
            FileMode.CreateNew,
            privateFile: true
        );
    }

    private static Ed25519CertificateStore Load(
        string certificatePath,
        string privateKeyPath,
        string password,
        ILogger<Ed25519CertificateStore> logger
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

        var contents = File.ReadAllBytes(privateKeyPath);
        var isLegacy = LegacyEd25519Protection.IsLegacyPrivateKey(contents);
        var privateKey = isLegacy
            ? LegacyEd25519Protection.DecryptPrivateKey(contents, certificateBytes, password)
            : DecryptPrivateKey(contents, password);

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

        if (isLegacy)
        {
            MigratePrivateKey(privateKeyPath, privateKey, password, logger);
        }

        return new Ed25519CertificateStore(certificate, privateKey, password);
    }

    private static byte[] EncryptPrivateKey(
        Ed25519PrivateKeyParameters privateKey,
        string password,
        SecureRandom random
    )
    {
        return EncryptedPrivateKeyInfoFactory
            .CreateEncryptedPrivateKeyInfo(
                NistObjectIdentifiers.IdAes256Cbc,
                PkcsObjectIdentifiers.IdHmacWithSha512,
                password.ToCharArray(),
                SecureRandom.GetNextBytes(random, SaltSize),
                Pbkdf2Iterations,
                random,
                privateKey
            )
            .GetEncoded(Asn1Encodable.Der);
    }

    private static Ed25519PrivateKeyParameters DecryptPrivateKey(byte[] contents, string password)
    {
        try
        {
            if (
                PrivateKeyFactory.DecryptKey(password.ToCharArray(), contents)
                is not Ed25519PrivateKeyParameters privateKey
            )
            {
                throw new CryptographicException(
                    "The Ed25519 private key file does not hold an Ed25519 key."
                );
            }

            return privateKey;
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException(
                "The Ed25519 private key file could not be decrypted.",
                ex
            );
        }
    }

    internal static void MigratePrivateKey(
        string privateKeyPath,
        Ed25519PrivateKeyParameters privateKey,
        string password,
        ILogger<Ed25519CertificateStore> logger
    )
    {
        var temporaryPath = privateKeyPath + MigrationFileSuffix;
        try
        {
            WriteFile(
                temporaryPath,
                EncryptPrivateKey(privateKey, password, new SecureRandom()),
                FileMode.Create,
                privateFile: true
            );
            File.Move(temporaryPath, privateKeyPath, overwrite: true);
            LogMigrated(logger);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogPrivateKeyMigrationFailed(logger, ex);
            DeleteMigrationFile(temporaryPath, logger);
        }
    }

    private static void DeleteMigrationFile(
        string temporaryPath,
        ILogger<Ed25519CertificateStore> logger
    )
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogMigrationFileLeftBehind(logger, ex);
        }
    }

    private static void WriteFile(string path, byte[] contents, FileMode mode, bool privateFile)
    {
        using var stream = new FileStream(
            path,
            mode,
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

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The Ed25519 Data Protection private key was rewritten as a PKCS#8 encrypted private key"
    )]
    private static partial void LogMigrated(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The Ed25519 Data Protection private key could not be rewritten as a PKCS#8 encrypted "
            + "private key; the previous file is still in use"
    )]
    private static partial void LogPrivateKeyMigrationFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The half-written Ed25519 Data Protection private key of the failed migration could "
            + "not be deleted and is still in the key volume"
    )]
    private static partial void LogMigrationFileLeftBehind(ILogger logger, Exception exception);
}

/// <summary>
/// Protects data-protection key elements as a CMS (RFC 5652) blob: the element is encrypted into an
/// <c>EnvelopedData</c> whose single <c>PasswordRecipientInfo</c> derives its key-encryption key from
/// the certificate password (PBKDF2, AES-256 RFC 3211 key wrap, AES-256-CBC content encryption), and
/// that blob is then wrapped in a <c>SignedData</c> signed with the Ed25519 certificate. No certificate
/// travels inside the blob: the signature is only ever checked against the certificate pinned in the
/// key volume, and that check runs before anything is decrypted.
/// </summary>
internal static class CmsKeyProtection
{
    internal const int SignedDataVersion = 1;
    internal const int SignerInfoVersion = 1;
    internal const int MaximumBlobSize = 1024 * 1024;

    internal static readonly XNamespace Namespace = "urn:codigoactivo:data-protection:cms:v2";
    internal const int ElementVersion = 2;

    internal static XElement Encrypt(XElement plaintextElement, Ed25519CertificateStore store)
    {
        var plaintext = Encoding.UTF8.GetBytes(
            plaintextElement.ToString(SaveOptions.DisableFormatting)
        );

        try
        {
            return new XElement(
                Namespace + "encryptedSecret",
                new XAttribute("version", ElementVersion),
                Convert.ToBase64String(Protect(plaintext, store))
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    internal static XElement Decrypt(XElement encryptedElement, Ed25519CertificateStore store)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        if (
            encryptedElement.Name != Namespace + "encryptedSecret"
            || !int.TryParse((string?)encryptedElement.Attribute("version"), out var version)
            || version != ElementVersion
        )
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.");
        }

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(encryptedElement.Value);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.", ex);
        }

        if (blob.Length is 0 or > MaximumBlobSize)
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.");
        }

        var plaintext = Unprotect(blob, store);
        try
        {
            return XElement.Parse(
                Encoding.UTF8.GetString(plaintext),
                LoadOptions.PreserveWhitespace
            );
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException("The decrypted Data Protection key is invalid.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] Protect(byte[] plaintext, Ed25519CertificateStore store)
    {
        var random = new SecureRandom();
        var envelopedGenerator = new CmsEnvelopedDataGenerator(random);
        envelopedGenerator.AddPasswordRecipient(
            new Pkcs5Scheme2Utf8PbeKey(
                store.CertificatePassword.ToCharArray(),
                SecureRandom.GetNextBytes(random, Ed25519CertificateStore.SaltSize),
                Ed25519CertificateStore.Pbkdf2Iterations
            ),
            CmsEnvelopedGenerator.Aes256Cbc
        );

        var enveloped = envelopedGenerator
            .Generate(new CmsProcessableByteArray(plaintext), NistObjectIdentifiers.IdAes256Cbc)
            .ContentInfo.GetEncoded(Asn1Encodable.Der);

        var signedGenerator = new CmsSignedDataGenerator(random) { UseDefiniteLength = true };
        signedGenerator.AddSignerInfoGenerator(
            new SignerInfoGeneratorBuilder().Build(
                new Asn1SignatureFactory("Ed25519", store.PrivateKey, random),
                store.Certificate
            )
        );

        return signedGenerator
            .Generate(new CmsProcessableByteArray(enveloped), encapsulate: true)
            .GetEncoded(Asn1Encodable.Der);
    }

    private static byte[] Unprotect(byte[] blob, Ed25519CertificateStore store)
    {
        try
        {
            return DecryptEnvelope(VerifySignature(blob, store), store);
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.", ex);
        }
    }

    private static byte[] VerifySignature(byte[] blob, Ed25519CertificateStore store)
    {
        var contentInfo = ContentInfo.GetInstance(Asn1Object.FromByteArray(blob));
        if (!PkcsObjectIdentifiers.SignedData.Equals(contentInfo.ContentType))
        {
            throw Invalid();
        }

        var signedData = new CmsSignedData(contentInfo);
        if (
            signedData.Version != SignedDataVersion
            || !PkcsObjectIdentifiers.Data.Equals(signedData.SignedContentType)
        )
        {
            throw Invalid();
        }

        var digests = signedData.GetDigestAlgorithms().ToList();
        if (digests.Count != 1 || !NistObjectIdentifiers.IdSha512.Equals(digests[0].Algorithm))
        {
            throw Invalid();
        }

        var signers = signedData.GetSignerInfos().GetSigners();
        if (signers.Count != 1)
        {
            throw Invalid();
        }

        var signer = signers[0];
        if (
            signer.Version != SignerInfoVersion
            || !NistObjectIdentifiers.IdSha512.Equals(signer.DigestAlgorithmID.Algorithm)
            || !EdECObjectIdentifiers.id_Ed25519.Equals(signer.SignatureAlgorithm.Algorithm)
            || !signer.SignerID.Match(store.Certificate)
        )
        {
            throw Invalid();
        }

        if (
            signedData.GetCertificates().EnumerateMatches(null).Any()
            || signedData.GetCrls().EnumerateMatches(null).Any()
        )
        {
            throw Invalid();
        }

        if (!signer.Verify(store.Certificate))
        {
            throw new CryptographicException(
                "The encrypted Data Protection key has an invalid Ed25519 signature."
            );
        }

        using var content = new MemoryStream();
        signedData.SignedContent.Write(content);
        return content.ToArray();
    }

    private static byte[] DecryptEnvelope(byte[] enveloped, Ed25519CertificateStore store)
    {
        var contentInfo = ContentInfo.GetInstance(Asn1Object.FromByteArray(enveloped));
        if (!PkcsObjectIdentifiers.EnvelopedData.Equals(contentInfo.ContentType))
        {
            throw Invalid();
        }

        var envelopedData = new CmsEnvelopedData(contentInfo);
        if (
            !NistObjectIdentifiers.IdAes256Cbc.Equals(envelopedData.EncryptionAlgorithmID.Algorithm)
        )
        {
            throw Invalid();
        }

        var recipients = envelopedData.GetRecipientInfos().GetRecipients();
        if (recipients.Count != 1 || recipients[0] is not PasswordRecipientInformation recipient)
        {
            throw Invalid();
        }

        var keyEncryption = recipient.KeyEncryptionAlgorithmID;
        if (
            !PkcsObjectIdentifiers.IdAlgPwriKek.Equals(keyEncryption.Algorithm)
            || !NistObjectIdentifiers.IdAes256Cbc.Equals(
                AlgorithmIdentifier.GetInstance(keyEncryption.Parameters).Algorithm
            )
            || !PkcsObjectIdentifiers.IdPbkdf2.Equals(recipient.KeyDerivationAlgorithm.Algorithm)
        )
        {
            throw Invalid();
        }

        var derivation = Pbkdf2Params.GetInstance(recipient.KeyDerivationAlgorithm.Parameters);
        if (
            derivation.IterationCount.CompareTo(
                BigInteger.ValueOf(Ed25519CertificateStore.Pbkdf2Iterations)
            ) < 0
        )
        {
            throw Invalid();
        }

        return recipient.GetContent(
            new Pkcs5Scheme2Utf8PbeKey(
                store.CertificatePassword.ToCharArray(),
                recipient.KeyDerivationAlgorithm
            )
        );
    }

    private static CryptographicException Invalid()
    {
        return new CryptographicException("The encrypted Data Protection key is invalid.");
    }
}

/// <summary>
/// Encrypts data-protection key elements into the CMS container described by
/// <see cref="CmsKeyProtection"/>.
/// </summary>
/// <param name="certificateStore">Store that provides the certificate, signing key and password.</param>
public sealed class Ed25519CmsXmlEncryptor(Ed25519CertificateStore certificateStore) : IXmlEncryptor
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
            CmsKeyProtection.Encrypt(plaintextElement, certificateStore),
            typeof(Ed25519CmsXmlDecryptor)
        );
    }
}

/// <summary>
/// Verifies and decrypts the CMS container written by <see cref="Ed25519CmsXmlEncryptor"/>.
/// </summary>
public sealed class Ed25519CmsXmlDecryptor : IXmlDecryptor
{
    private readonly Ed25519CertificateStore certificateStore;

    /// <summary>
    /// Initializes a CMS xml decryptor with its required dependencies.
    /// </summary>
    /// <param name="services">Service collection or provider used to resolve dependencies.</param>
    public Ed25519CmsXmlDecryptor(IServiceProvider services)
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
        return CmsKeyProtection.Decrypt(encryptedElement, certificateStore);
    }
}
