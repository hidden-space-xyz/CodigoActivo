using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace CodigoActivo.API.Security;

/// <summary>
/// Reads the retired v1 Data Protection formats. Key files written before the move to PKCS#8 and CMS
/// name this type, so it must keep its name and namespace; nothing writes these formats any more.
/// The v1 private-key JSON envelope is PBKDF2-HMAC-SHA-512 over AES-256-GCM with the certificate as
/// associated data, and a v1 key element is AES-256-GCM under a key derived from the signing key with
/// HKDF-SHA-256, signed with Ed25519 over <c>version‖nonce‖tag‖ciphertext</c>.
/// </summary>
internal static class LegacyEd25519Protection
{
    private const int PasswordIterations = 600_000;
    private const int SaltSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int EncryptionKeySize = 32;
    private const int MaximumCiphertextSize = 1024 * 1024;

    private static readonly XNamespace Namespace =
        "urn:codigoactivo:data-protection:ed25519:v1";
    private static readonly byte[] AdditionalData = Encoding.UTF8.GetBytes(
        "CodigoActivo Data Protection XML v1"
    );
    private static readonly byte[] KeyDerivationContext = Encoding.UTF8.GetBytes(
        "CodigoActivo Ed25519 certificate key wrapping v1"
    );

    internal static bool IsLegacyPrivateKey(ReadOnlySpan<byte> contents)
    {
        return contents.Length > 0 && contents[0] == (byte)'{';
    }

    internal static Ed25519PrivateKeyParameters DecryptPrivateKey(
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
        var encryptionKey = DerivePasswordKey(password, salt, envelope.Iterations);
        byte[]? plaintext = null;

        try
        {
            plaintext = DecryptGcm(encryptionKey, nonce, ciphertext, tag, certificate);
            return new Ed25519PrivateKeyParameters(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(ciphertext);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    internal static XElement Decrypt(XElement encryptedElement, Ed25519CertificateStore store)
    {
        ArgumentNullException.ThrowIfNull(encryptedElement);

        if (
            encryptedElement.Name != Namespace + "encryptedSecret"
            || !int.TryParse((string?)encryptedElement.Attribute("version"), out var version)
            || version != 1
        )
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.");
        }

        var nonce = DecodeElement(encryptedElement, "nonce", NonceSize);
        var tag = DecodeElement(encryptedElement, "tag", TagSize);
        var ciphertext = DecodeElement(encryptedElement, "ciphertext", expectedSize: null);
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
            var verifier = new Ed25519Signer();
            verifier.Init(forSigning: false, store.Certificate.GetPublicKey());
            verifier.BlockUpdate(signedPayload, 0, signedPayload.Length);
            if (!verifier.VerifySignature(signature))
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

        var encryptionKey = DeriveElementKey(store.PrivateKey);
        byte[]? plaintext = null;
        try
        {
            plaintext = DecryptGcm(encryptionKey, nonce, ciphertext, tag, AdditionalData);
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
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(ciphertext);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    private static byte[] DerivePasswordKey(string password, byte[] salt, int iterations)
    {
        var generator = new Pkcs5S2ParametersGenerator(new Sha512Digest());
        generator.Init(
            PbeParametersGenerator.Pkcs5PasswordToUtf8Bytes(password.ToCharArray()),
            salt,
            iterations
        );
        return (
            (KeyParameter)generator.GenerateDerivedParameters("AES", EncryptionKeySize * 8)
        ).GetKey();
    }

    private static byte[] DeriveElementKey(Ed25519PrivateKeyParameters privateKey)
    {
        var privateKeyBytes = privateKey.GetEncoded();
        try
        {
            var generator = new HkdfBytesGenerator(new Sha256Digest());
            generator.Init(
                new HkdfParameters(
                    privateKeyBytes,
                    privateKey.GeneratePublicKey().GetEncoded(),
                    KeyDerivationContext
                )
            );
            var key = new byte[EncryptionKeySize];
            generator.GenerateBytes(key, 0, key.Length);
            return key;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyBytes);
        }
    }

    private static byte[] DecryptGcm(
        byte[] key,
        byte[] nonce,
        byte[] ciphertext,
        byte[] tag,
        byte[] associatedData
    )
    {
        var input = new byte[ciphertext.Length + tag.Length];
        ciphertext.CopyTo(input, 0);
        tag.CopyTo(input, ciphertext.Length);

        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(
            forEncryption: false,
            new AeadParameters(new KeyParameter(key), TagSize * 8, nonce, associatedData)
        );

        var output = new byte[cipher.GetOutputSize(input.Length)];
        try
        {
            var written = cipher.ProcessBytes(input, 0, input.Length, output, 0);
            written += cipher.DoFinal(output, written);
            if (written != ciphertext.Length)
            {
                throw new CryptographicException(
                    "The encrypted Data Protection key is invalid."
                );
            }

            return output;
        }
        catch (Exception ex) when (ex is not CryptographicException)
        {
            CryptographicOperations.ZeroMemory(output);
            throw new CryptographicException(
                "The encrypted Data Protection key is invalid.",
                ex
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
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

    private static byte[] DecodeElement(XElement parent, string name, int? expectedSize)
    {
        var element = parent.Element(Namespace + name);
        if (element is null)
        {
            throw new CryptographicException("The encrypted Data Protection key is invalid.");
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(element.Value);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException(
                "The encrypted Data Protection key is invalid.",
                ex
            );
        }

        if (expectedSize is null || decoded.Length == expectedSize)
        {
            return decoded;
        }

        CryptographicOperations.ZeroMemory(decoded);
        throw new CryptographicException("The encrypted Data Protection key is invalid.");
    }

    private static byte[] Decode(string value, int expectedSize, string field)
    {
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException(
                $"The Ed25519 private key {field} is invalid.",
                ex
            );
        }

        if (decoded.Length == expectedSize)
        {
            return decoded;
        }

        CryptographicOperations.ZeroMemory(decoded);
        throw new CryptographicException(
            $"The Ed25519 private key {field} has an invalid length."
        );
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

/// <summary>
/// Decrypts data-protection key elements written in the retired v1 Ed25519 format. Existing key files
/// record this assembly-qualified type name, so it is kept for reading only; new keys are protected by
/// <see cref="Ed25519CmsXmlEncryptor"/>.
/// </summary>
public sealed class Ed25519XmlDecryptor : IXmlDecryptor
{
    private readonly Ed25519CertificateStore certificateStore;

    /// <summary>
    /// Initializes an ed25519 xml decryptor with its required dependencies.
    /// </summary>
    /// <param name="services">Service collection or provider used to resolve dependencies.</param>
    public Ed25519XmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        certificateStore = services.GetRequiredService<Ed25519CertificateStore>();
    }

    /// <summary>
    /// Verifies and decrypts the protected XML element.
    /// </summary>
    /// <param name="encryptedElement">Protected XML element to verify and decrypt.</param>
    /// <returns>The resulting x element value.</returns>
    public XElement Decrypt(XElement encryptedElement)
    {
        return LegacyEd25519Protection.Decrypt(encryptedElement, certificateStore);
    }
}
