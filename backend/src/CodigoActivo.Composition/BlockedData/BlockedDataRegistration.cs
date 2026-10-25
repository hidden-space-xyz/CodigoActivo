using CodigoActivo.Application.BlockedData;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.BlockedData;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.Infrastructure.DataHistory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.BlockedData;

/// <summary>
/// Registers the blocked data: the data history with its reader and scheduled purge, the account
/// designated for data protection and the consultations it opens.
/// </summary>
internal static class BlockedDataRegistration
{
    /// <summary>
    /// Adds the blocked data services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddBlockedData(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var settings = new Settings(configuration);
        services.AddValidatedOptions<PrivacyOfficerOptions>(
            options =>
                options.Email = EmailAddress.Create(
                    settings.Text("PRIVACY_OFFICER_EMAIL", string.Empty)
                )
                    is { IsSuccess: true } email
                    ? email.Value.Value
                    : string.Empty,
            options => options.Email.Length > 0,
            "PRIVACY_OFFICER_EMAIL must be the email address of the account designated for data protection."
        );
        services.AddScoped<PrivacyOfficer>();
        services.AddScoped<IBlockedDataConsultationRepository, BlockedDataConsultationRepository>();
        services.AddScoped<IDataHistoryReader, DataHistoryReader>();
        services.AddSingleton<IBlockedDataEmailComposer, BlockedDataEmailComposer>();
        services.AddFixedOptions<DataHistoryRetentionOptions>();
        services.AddScoped<IDataHistoryRetention, DataHistoryRetention>();
        services.AddHostedService<DataHistoryPurgeWorker>();
        return services;
    }
}
