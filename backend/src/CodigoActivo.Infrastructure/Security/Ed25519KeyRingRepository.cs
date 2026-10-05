using System.Xml.Linq;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Reads the key ring through another repository and keeps only the keys whose secrets the Ed25519
/// certificate protects: every encrypted secret of the key must name
/// <see cref="Ed25519AesGcmXmlDecryptor"/>, and no master key may be stored in clear. Otherwise
/// anyone able to write to the key volume, without knowing the certificate password, could plant a
/// key in clear and have it used to protect sessions and stored secrets. Elements that are not keys,
/// such as revocations, are kept unchanged, and storing is delegated as is.
/// </summary>
/// <param name="inner">Repository that stores the key ring.</param>
/// <param name="logger">Logger used to record the keys that are ignored.</param>
public sealed class Ed25519KeyRingRepository(
    IXmlRepository inner,
    ILogger<Ed25519KeyRingRepository> logger
) : IXmlRepository
{
    private const string MasterKeyName = "masterKey";

    private static readonly XName KeyName = "key";
    private static readonly XName DecryptorTypeAttributeName = "decryptorType";
    private static readonly XNamespace DataProtectionNamespace =
        "http://schemas.asp.net/2015/03/dataProtection";
    private static readonly XName EncryptedSecretName = DataProtectionNamespace + "encryptedSecret";
    private static readonly XName RequiresEncryptionName =
        DataProtectionNamespace + "requiresEncryption";
    private static readonly string DecryptorTypeName = typeof(Ed25519AesGcmXmlDecryptor).FullName!;
    private static readonly string DecryptorAssemblyName = typeof(Ed25519AesGcmXmlDecryptor)
        .Assembly.GetName()
        .Name!;

    /// <summary>
    /// Reads every element of the key ring except the keys the certificate does not protect.
    /// </summary>
    /// <returns>The accepted elements.</returns>
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var elements = inner.GetAllElements();
        var accepted = elements.Where(IsAccepted).ToList();
        if (accepted.Count != elements.Count)
        {
            logger.DataProtectionKeysIgnored(elements.Count - accepted.Count);
        }

        return accepted;
    }

    /// <summary>
    /// Stores an element of the key ring through the inner repository.
    /// </summary>
    /// <param name="element">Element to store.</param>
    /// <param name="friendlyName">Name the inner repository may use for the element.</param>
    public void StoreElement(XElement element, string friendlyName)
    {
        inner.StoreElement(element, friendlyName);
    }

    private static bool IsAccepted(XElement element)
    {
        if (element.Name != KeyName)
        {
            return true;
        }

        var secrets = element.Descendants(EncryptedSecretName).ToList();
        return secrets.Count > 0
            && secrets.TrueForAll(IsDecryptedByTheCertificate)
            && !element.Descendants().Any(IsSecretInClear);
    }

    private static bool IsDecryptedByTheCertificate(XElement secret)
    {
        var parts = ((string?)secret.Attribute(DecryptorTypeAttributeName))?.Split(',');
        return parts is { Length: >= 2 }
            && string.Equals(parts[0].Trim(), DecryptorTypeName, StringComparison.Ordinal)
            && string.Equals(parts[1].Trim(), DecryptorAssemblyName, StringComparison.Ordinal);
    }

    private static bool IsSecretInClear(XElement element)
    {
        return element.Name.LocalName == MasterKeyName
            || element.Attribute(RequiresEncryptionName) is not null;
    }
}
