using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Configures how ASP.NET Data Protection encrypts payloads and protects its key ring at rest.
/// </summary>
public static class DataProtectionKeyProtection
{
    /// <summary>
    /// Encrypts every protected payload with AES-256-GCM.
    /// </summary>
    /// <param name="dataProtection">Data Protection builder to configure.</param>
    /// <returns>The same builder.</returns>
    public static IDataProtectionBuilder ProtectPayloadsWithAesGcm(
        this IDataProtectionBuilder dataProtection
    )
    {
        ArgumentNullException.ThrowIfNull(dataProtection);

        return dataProtection.UseCryptographicAlgorithms(
            new AuthenticatedEncryptorConfiguration
            {
                EncryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM,
            }
        );
    }

    /// <summary>
    /// Persists the key ring to <paramref name="keysDirectory"/> and encrypts every key with
    /// <see cref="AesGcmKeyRingProtection"/> under <paramref name="rootKey"/>. Keys found in the
    /// directory that this protection does not cover are ignored when the ring is read, and a
    /// stored element that the root key does not decrypt stops the registration.
    /// </summary>
    /// <param name="dataProtection">Data Protection builder to configure.</param>
    /// <param name="keysDirectory">Directory holding the key ring.</param>
    /// <param name="rootKey">Root key the key ring key is derived from.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// A stored element does not decrypt with <paramref name="rootKey"/>.
    /// </exception>
    public static IDataProtectionBuilder ProtectKeysWithRootKey(
        this IDataProtectionBuilder dataProtection,
        DirectoryInfo keysDirectory,
        DataProtectionRootKey rootKey
    )
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        ArgumentNullException.ThrowIfNull(keysDirectory);

        var protection = new AesGcmKeyRingProtection(rootKey);
        protection.VerifyKeyRing(keysDirectory);
        dataProtection.Services.AddSingleton(protection);
        dataProtection.PersistKeysToFileSystem(keysDirectory);
        dataProtection.Services.Configure<KeyManagementOptions>(options =>
            options.XmlEncryptor = new AesGcmXmlEncryptor(protection)
        );
        dataProtection
            .Services.AddOptions<KeyManagementOptions>()
            .PostConfigure<IServiceProvider>(
                (options, provider) =>
                    options.XmlRepository = new ProtectedKeyRingRepository(
                        options.XmlRepository!,
                        provider.GetService<ILogger<ProtectedKeyRingRepository>>()
                            ?? NullLogger<ProtectedKeyRingRepository>.Instance
                    )
            );
        return dataProtection;
    }
}
