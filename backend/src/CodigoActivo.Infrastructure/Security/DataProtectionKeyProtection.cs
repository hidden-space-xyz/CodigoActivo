using CodigoActivo.Application.Abstractions.Time;
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
                ValidationAlgorithm = ValidationAlgorithm.HMACSHA512,
            }
        );
    }

    /// <summary>
    /// Persists the key ring to <paramref name="keysDirectory"/> and encrypts every key with the
    /// Ed25519 certificate stored next to it, creating that certificate on first use. Keys found in
    /// the directory that the certificate does not protect are ignored when the ring is read.
    /// </summary>
    /// <param name="dataProtection">Data Protection builder to configure.</param>
    /// <param name="services">Service collection that receives the certificate store.</param>
    /// <param name="keysDirectory">Directory holding the key ring and the certificate.</param>
    /// <param name="certificatePassword">Password protecting the certificate and the key ring.</param>
    /// <param name="clock">Clock that dates a newly created certificate.</param>
    /// <returns>The loaded certificate store.</returns>
    public static Ed25519CertificateStore ProtectKeysWithEd25519Certificate(
        this IDataProtectionBuilder dataProtection,
        IServiceCollection services,
        DirectoryInfo keysDirectory,
        string certificatePassword,
        IClock clock
    )
    {
        ArgumentNullException.ThrowIfNull(dataProtection);
        ArgumentNullException.ThrowIfNull(services);

        var certificateStore = Ed25519CertificateStore.LoadOrCreate(
            keysDirectory,
            certificatePassword,
            clock
        );

        services.AddSingleton(certificateStore);
        dataProtection.PersistKeysToFileSystem(keysDirectory);
        services.Configure<KeyManagementOptions>(options =>
            options.XmlEncryptor = new Ed25519AesGcmXmlEncryptor(certificateStore)
        );
        services
            .AddOptions<KeyManagementOptions>()
            .PostConfigure<IServiceProvider>(
                (options, provider) =>
                    options.XmlRepository = new Ed25519KeyRingRepository(
                        options.XmlRepository!,
                        provider.GetService<ILogger<Ed25519KeyRingRepository>>()
                            ?? NullLogger<Ed25519KeyRingRepository>.Instance
                    )
            );
        return certificateStore;
    }
}
