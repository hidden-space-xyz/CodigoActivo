using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Reports;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.Infrastructure.News;
using CodigoActivo.Infrastructure.Partners;
using CodigoActivo.Infrastructure.Reports;
using CodigoActivo.Infrastructure.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Content;

/// <summary>
/// Registers the published content: news, partners, resources, the files they show and the
/// reports built on top of them.
/// </summary>
internal static class ContentRegistration
{
    /// <summary>
    /// Adds the content services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddContent(this IServiceCollection services)
    {
        services.AddScoped<INewsItemRepository, NewsItemRepository>();
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IDashboardCountsReader, DashboardCountsReader>();

        services.AddScoped<IStoredFileRepository, StoredFileRepository>();
        services.AddFixedOptions<FileUploadOptions>();
        services.AddFixedOptions<FileStorageOptions>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<FileUploadValidator>();
        services.AddScoped<IOrphanFileCleaner, OrphanFileCleaner>();
        services.AddFixedOptions<UnusedFilePurgeOptions>();
        services.AddHostedService<UnusedFilePurgeWorker>();
        return services;
    }
}
