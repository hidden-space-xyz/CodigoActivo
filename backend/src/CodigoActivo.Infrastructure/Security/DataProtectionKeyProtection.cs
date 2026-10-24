using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
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
    /// Keeps the key ring in the directory of the registered <see cref="KeyRingOptions"/> and
    /// encrypts every key with <see cref="AesGcmKeyRingProtection"/> under the registered
    /// <see cref="DataProtectionRootKey"/>. Keys found in the directory that this protection does not
    /// cover are ignored when the ring is read.
    /// </summary>
    /// <param name="dataProtection">Data Protection builder to configure.</param>
    /// <returns>The same builder.</returns>
    public static IDataProtectionBuilder ProtectKeysWithRootKey(
        this IDataProtectionBuilder dataProtection
    )
    {
        ArgumentNullException.ThrowIfNull(dataProtection);

        dataProtection.Services.AddSingleton<AesGcmKeyRingProtection>();
        dataProtection
            .Services.AddOptions<KeyManagementOptions>()
            .Configure<IServiceProvider>(
                (options, provider) =>
                {
                    var loggers =
                        provider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
                    var directory = new DirectoryInfo(
                        provider.GetRequiredService<KeyRingOptions>().DirectoryPath
                    );
                    options.XmlRepository = new ProtectedKeyRingRepository(
                        new FileSystemXmlRepository(directory, loggers),
                        loggers.CreateLogger<ProtectedKeyRingRepository>()
                    );
                    options.XmlEncryptor = new AesGcmXmlEncryptor(
                        provider.GetRequiredService<AesGcmKeyRingProtection>()
                    );
                }
            );
        return dataProtection;
    }
}
