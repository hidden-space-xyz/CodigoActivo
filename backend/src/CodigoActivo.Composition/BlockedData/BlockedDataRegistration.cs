using CodigoActivo.Application.BlockedData;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Infrastructure.BlockedData;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.Infrastructure.DataHistory;
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
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddBlockedData(this IServiceCollection services)
    {
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
