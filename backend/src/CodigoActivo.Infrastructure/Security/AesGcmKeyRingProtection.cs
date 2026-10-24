using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Protects Data Protection key ring elements with AES-256-GCM under the key that
/// <see cref="DataProtectionRootKey"/> derives for <see cref="KeyPurpose"/>. Each element carries a
/// fresh random nonce, the authentication tag and the ciphertext; the tag rejects an element that was
/// changed or written without the root key before any byte of it is returned.
/// </summary>
public sealed class AesGcmKeyRingProtection
{
    /// <summary>
    /// Label that separates the key ring key from any other key derived from the root key.
    /// </summary>
    public const string KeyPurpose = "CodigoActivo.DataProtection.KeyRing.v1";

    private const int ElementVersion = 1;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MaximumCiphertextSize = 1024 * 1024;
    private const string ElementName = "encryptedSecret";
    private const string VersionAttributeName = "version";

    private static readonly XNamespace Namespace = "urn:codigoactivo:data-protection:aes-gcm:v1";
    private static readonly byte[] AdditionalData = Encoding.UTF8.GetBytes(
        "CodigoActivo Data Protection key element v1"
    );

    private readonly byte[] key;

    /// <summary>
    /// Initializes the protection with the key ring key derived from <paramref name="rootKey"/>.
    /// </summary>
    /// <param name="rootKey">Root key of the deployment.</param>
    public AesGcmKeyRingProtection(DataProtectionRootKey rootKey)
    {
        ArgumentNullException.ThrowIfNull(rootKey);
        key = rootKey.DeriveKey(KeyPurpose, KeySize);
    }

    /// <summary>
    /// Decrypts every key ring element stored in <paramref name="keysDirectory"/>, so a root key that
    /// does not open the existing ring stops the start instead of letting Data Protection replace the
    /// ring with new keys.
    /// </summary>
    /// <param name="keysDirectory">Directory holding the key ring.</param>
    /// <exception cref="CryptographicException">A stored element does not decrypt with the root key.</exception>
    internal void VerifyKeyRing(DirectoryInfo keysDirectory)
    {
        if (!keysDirectory.Exists)
        {
            return;
        }

        foreach (var file in keysDirectory.EnumerateFiles("*.xml"))
        {
            foreach (
                var element in XElement.Load(file.FullName).Descendants(Namespace + ElementName)
            )
            {
                try
                {
                    Decrypt(element);
                }
                catch (CryptographicException ex)
                {
                    throw new CryptographicException(
                        "DATA_PROTECTION_KEY does not decrypt the key ring in api-dataprotection.",
                        ex
                    );
                }
            }
        }
    }

    internal XElement Encrypt(XElement plaintextElement)
    {
        var plaintext = Encoding.UTF8.GetBytes(
            plaintextElement.ToString(SaveOptions.DisableFormatting)
        );
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        try
        {
            using var cipher = new AesGcm(key, TagSize);
            cipher.Encrypt(nonce, plaintext, ciphertext, tag, AdditionalData);

            return new XElement(
                Namespace + ElementName,
                new XAttribute(VersionAttributeName, ElementVersion),
                new XElement(Namespace + "nonce", Convert.ToBase64String(nonce)),
                new XElement(Namespace + "tag", Convert.ToBase64String(tag)),
                new XElement(Namespace + "ciphertext", Convert.ToBase64String(ciphertext))
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    internal XElement Decrypt(XElement encryptedElement)
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

        var nonce = Decode(encryptedElement, "nonce", NonceSize);
        var tag = Decode(encryptedElement, "tag", TagSize);
        var ciphertext = Decode(encryptedElement, "ciphertext", expectedSize: null);

        if (ciphertext.Length is 0 or > MaximumCiphertextSize)
        {
            throw Invalid();
        }

        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var cipher = new AesGcm(key, TagSize);
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
            CryptographicOperations.ZeroMemory(plaintext);
        }
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

        throw Invalid();
    }

    private static CryptographicException Invalid()
    {
        return new CryptographicException("The encrypted Data Protection key is invalid.");
    }
}

/// <summary>
/// Encrypts Data Protection key ring elements into the AES-256-GCM element described by
/// <see cref="AesGcmKeyRingProtection"/>.
/// </summary>
public sealed class AesGcmXmlEncryptor : IXmlEncryptor
{
    private readonly AesGcmKeyRingProtection protection;

    /// <summary>
    /// Initializes the encryptor with the protection that holds the key ring key.
    /// </summary>
    /// <param name="protection">Protection that holds the key ring key.</param>
    public AesGcmXmlEncryptor(AesGcmKeyRingProtection protection)
    {
        ArgumentNullException.ThrowIfNull(protection);
        this.protection = protection;
    }

    /// <summary>
    /// Encrypts the XML element for Data Protection storage.
    /// </summary>
    /// <param name="plaintextElement">Unencrypted XML element to protect.</param>
    /// <returns>The encrypted element and the decryptor that reads it back.</returns>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        ArgumentNullException.ThrowIfNull(plaintextElement);

        return new EncryptedXmlInfo(
            protection.Encrypt(plaintextElement),
            typeof(AesGcmXmlDecryptor)
        );
    }
}

/// <summary>
/// Decrypts the elements written by <see cref="AesGcmXmlEncryptor"/>. Data Protection resolves this
/// type by the name recorded in each key file.
/// </summary>
public sealed class AesGcmXmlDecryptor : IXmlDecryptor
{
    private readonly AesGcmKeyRingProtection protection;

    /// <summary>
    /// Initializes the decryptor with its required dependencies.
    /// </summary>
    /// <param name="services">Service provider that holds the key ring protection.</param>
    public AesGcmXmlDecryptor(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        protection = services.GetRequiredService<AesGcmKeyRingProtection>();
    }

    /// <summary>
    /// Authenticates and decrypts the element.
    /// </summary>
    /// <param name="encryptedElement">Protected XML element to decrypt.</param>
    /// <returns>The decrypted element.</returns>
    public XElement Decrypt(XElement encryptedElement)
    {
        return protection.Decrypt(encryptedElement);
    }
}
