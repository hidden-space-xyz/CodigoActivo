using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
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

public sealed class Ed25519CertificateStore
{
    private const int PasswordIterations = 600_000;
    private const int SaltSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int EncryptionKeySize = 32;

    private readonly Ed25519PrivateKeyParameters privateKey;

    private Ed25519CertificateStore(
        X509Certificate certificate,
        Ed25519PrivateKeyParameters privateKey
    )
    {
        Certificate = certificate;
        this.privateKey = privateKey;
    }

    public X509Certificate Certificate { get; }

    public static Ed25519CertificateStore LoadOrCreate(
        DirectoryInfo directory,
        string password
    )
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        directory.Create();
        var certificatePath = Path.Combine(directory.FullName, "key-encryption-ed25519.cer");
        var privateKeyPath = Path.Combine(directory.FullName, "key-encryption-ed25519.key");
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

    internal byte[] DeriveEncryptionKey()
    {
        var privateKeyBytes = privateKey.GetEncoded();
        var publicKeyBytes = privateKey.GeneratePublicKey().GetEncoded();
        try
        {
            return HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                privateKeyBytes,
                EncryptionKeySize,
                publicKeyBytes,
                Ed25519XmlEncryptor.KeyDerivationContext
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyBytes);
        }
    }

    internal byte[] Sign(ReadOnlySpan<byte> data)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, privateKey);
        signer.BlockUpdate(data);
        return signer.GenerateSignature();
    }

    internal bool Verify(ReadOnlySpan<byte> data, byte[] signature)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: false, Certificate.GetPublicKey());
        signer.BlockUpdate(data);
        return signer.VerifySignature(signature);
    }

    private static void Create(string certificatePath, string privateKeyPath, string password)
    {
        var random = new SecureRandom();
        var privateKey = new Ed25519PrivateKeyParameters(random);
        var publicKey = privateKey.GeneratePublicKey();
        var name = new X509Name("CN=CodigoActivo Data Protection");
        var generator = new X509V3CertificateGenerator();

        generator.SetSerialNumber(new BigInteger(1, RandomNumberGenerator.GetBytes(20)));
        generator.SetIssuerDN(name);
        generator.SetSubjectDN(name);
        generator.SetNotBefore(DateTime.UtcNow.AddMinutes(-5));
        generator.SetNotAfter(DateTime.UtcNow.AddYears(20));
        generator.SetPublicKey(publicKey);
        generator.AddExtension(X509Extensions.BasicConstraints, critical: true, new BasicConstraints(false));
        generator.AddExtension(
            X509Extensions.KeyUsage,
            critical: true,
            new KeyUsage(KeyUsage.DigitalSignature)
        );

        var certificate = generator.Generate(new Asn1SignatureFactory("Ed25519", privateKey, random));
        certificate.Verify(publicKey);

        var certificateBytes = certificate.GetEncoded();
        var privateKeyBytes = privateKey.GetEncoded();
        try
        {
            WriteNewFile(certificatePath, certificateBytes, privateFile: false);
            WriteNewFile(
                privateKeyPath,
                ProtectPrivateKey(privateKeyBytes, certificateBytes, password),
                privateFile: true
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyBytes);
        }
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
            )
            || certificate.GetPublicKey() is not Ed25519PublicKeyParameters publicKey
        )
        {
            throw new InvalidOperationException(
                "The Data Protection certificate must use Ed25519."
            );
        }

        certificate.Verify(publicKey);
        var privateKeyBytes = UnprotectPrivateKey(
            File.ReadAllBytes(privateKeyPath),
            certificateBytes,
            password
        );
        try
        {
            var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes);
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

            return new Ed25519CertificateStore(certificate, privateKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyBytes);
        }
    }

    private static byte[] ProtectPrivateKey(
        byte[] privateKey,
        byte[] certificate,
        string password
    )
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[privateKey.Length];
        var encryptionKey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA512,
            EncryptionKeySize
        );

        try
        {
            using var aes = new AesGcm(encryptionKey, TagSize);
            aes.Encrypt(nonce, privateKey, ciphertext, tag, certificate);
            return JsonSerializer.SerializeToUtf8Bytes(
                new PrivateKeyEnvelope(
                    Version: 1,
                    Iterations: PasswordIterations,
                    Salt: Convert.ToBase64String(salt),
                    Nonce: Convert.ToBase64String(nonce),
                    Tag: Convert.ToBase64String(tag),
                    Ciphertext: Convert.ToBase64String(ciphertext)
                )
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private static byte[] UnprotectPrivateKey(
        byte[] envelopeBytes,
        byte[] certificate,
        string password
    )
    {
        PrivateKeyEnvelope envelope;
        try
        {
            envelope =
                JsonSerializer.Deserialize<PrivateKeyEnvelope>(envelopeBytes)
                ?? throw new CryptographicException("The Ed25519 private key file is empty.");
        }
        catch (JsonException ex)
        {
            throw new CryptographicException("The Ed25519 private key file is invalid.", ex);
        }

        if (envelope.Version != 1 || envelope.Iterations != PasswordIterations)
        {
            throw new CryptographicException("The Ed25519 private key format is unsupported.");
        }

        var salt = Decode(envelope.Salt, SaltSize, "salt");
        var nonce = Decode(envelope.Nonce, NonceSize, "nonce");
        var tag = Decode(envelope.Tag, TagSize, "authentication tag");
        var ciphertext = Decode(
            envelope.Ciphertext,
            Ed25519PrivateKeyParameters.KeySize,
            "ciphertext"
        );
        var plaintext = new byte[ciphertext.Length];
        var encryptionKey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            envelope.Iterations,
            HashAlgorithmName.SHA512,
            EncryptionKeySize
        );

        try
        {
            using var aes = new AesGcm(encryptionKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, certificate);
            return plaintext;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private static byte[] Decode(string value, int expectedSize, string field)
    {
        try
        {
            var decoded = Convert.FromBase64String(value);
            if (decoded.Length == expectedSize)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
        }

        throw new CryptographicException($"The Ed25519 private key {field} is invalid.");
    }

    private static void WriteNewFile(string path, byte[] contents, bool privateFile)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough
        );
        stream.Write(contents);
        stream.Flush(flushToDisk: true);

        if (privateFile && OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed record PrivateKeyEnvelope(
        int Version,
        int Iterations,
        string Salt,
        string Nonce,
        string Tag,
        string Ciphertext
    );
}

public sealed class Ed25519XmlEncryptor(Ed25519CertificateStore certificateStore)
    : IXmlEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MaximumCiphertextSize = 1024 * 1024;
    private static readonly XNamespace Namespace =
        "urn:codigoactivo:data-protection:ed25519:v1";
    private static readonly byte[] AdditionalData = Encoding.UTF8.GetBytes(
        "CodigoActivo Data Protection XML v1"
    );

    internal static readonly byte[] KeyDerivationContext = Encoding.UTF8.GetBytes(
        "CodigoActivo Ed25519 certificate key wrapping v1"
    );

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        var plaintext = Encoding.UTF8.GetBytes(
            plaintextElement.ToString(SaveOptions.DisableFormatting)
        );
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var encryptionKey = certificateStore.DeriveEncryptionKey();

        try
        {
            using var aes = new AesGcm(encryptionKey, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, AdditionalData);
            var signedPayload = BuildSignedPayload(nonce, tag, ciphertext);
            try
            {
                var signature = certificateStore.Sign(signedPayload);
                var encryptedElement = new XElement(
                    Namespace + "encryptedSecret",
                    new XAttribute("version", 1),
                    new XElement(Namespace + "nonce", Convert.ToBase64String(nonce)),
                    new XElement(Namespace + "tag", Convert.ToBase64String(tag)),
                    new XElement(
                        Namespace + "ciphertext",
                        Convert.ToBase64String(ciphertext)
                    ),
                    new XElement(
                        Namespace + "signature",
                        Convert.ToBase64String(signature)
                    )
                );
                return new EncryptedXmlInfo(encryptedElement, typeof(Ed25519XmlDecryptor));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(signedPayload);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    internal static XElement Decrypt(
        XElement encryptedElement,
        Ed25519CertificateStore certificateStore
    )
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);
        if (
            encryptedElement.Name != Namespace + "encryptedSecret"
            || (int?)encryptedElement.Attribute("version") != 1
        )
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.");
        }

        var nonce = DecodeElement(encryptedElement, "nonce", NonceSize);
        var tag = DecodeElement(encryptedElement, "tag", TagSize);
        var ciphertext = DecodeElement(
            encryptedElement,
            "ciphertext",
            expectedSize: null
        );
        var signature = DecodeElement(
            encryptedElement,
            "signature",
            Ed25519PrivateKeyParameters.SignatureSize
        );
        if (ciphertext.Length is 0 or > MaximumCiphertextSize)
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.");
        }

        var signedPayload = BuildSignedPayload(nonce, tag, ciphertext);
        try
        {
            if (!certificateStore.Verify(signedPayload, signature))
            {
                throw new CryptographicException(
                    "The encrypted Data Protection key has an invalid Ed25519 signature."
                );
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signedPayload);
        }

        var plaintext = new byte[ciphertext.Length];
        var encryptionKey = certificateStore.DeriveEncryptionKey();
        try
        {
            using var aes = new AesGcm(encryptionKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, AdditionalData);
            return XElement.Parse(Encoding.UTF8.GetString(plaintext), LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            throw new CryptographicException("The decrypted Data Protection key is invalid.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    private static byte[] DecodeElement(
        XElement parent,
        string name,
        int? expectedSize
    )
    {
        var element = parent.Element(Namespace + name);
        if (element is null)
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.");
        }

        try
        {
            var decoded = Convert.FromBase64String(element.Value);
            if (expectedSize is null || decoded.Length == expectedSize)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
        }

        throw new CryptographicException("The encrypted Data Protection key is invalid.");
    }

    private static byte[] BuildSignedPayload(byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        var payload = new byte[1 + nonce.Length + tag.Length + ciphertext.Length];
        payload[0] = 1;
        nonce.CopyTo(payload, 1);
        tag.CopyTo(payload, 1 + nonce.Length);
        ciphertext.CopyTo(payload, 1 + nonce.Length + tag.Length);
        return payload;
    }
}

public sealed class Ed25519XmlDecryptor : IXmlDecryptor
{
    private readonly Ed25519CertificateStore certificateStore;

    public Ed25519XmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        certificateStore = services.GetRequiredService<Ed25519CertificateStore>();
    }

    public XElement Decrypt(XElement encryptedElement)
    {
        return Ed25519XmlEncryptor.Decrypt(encryptedElement, certificateStore);
    }
}
