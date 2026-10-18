using CodigoActivo.Composition.Accounts;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Composition.Content;
using CodigoActivo.Composition.Emails;
using CodigoActivo.Composition.Events;
using CodigoActivo.Composition.Legal;
using CodigoActivo.Composition.Logs;
using CodigoActivo.Composition.Persistence;
using CodigoActivo.Composition.Platform;
using CodigoActivo.Composition.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition;

/// <summary>
/// Composition root of the application: the one place that wires the use cases to the
/// infrastructure that serves them.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the application with its infrastructure. Settings are read strictly and checked when
    /// the host starts; a production process is refused right away when its secrets are weak or its
    /// public address and mail delivery are unsafe.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configuration">Configuration of the application.</param>
    /// <param name="production">
    /// Whether the application runs in production, where the data protection keys are kept
    /// encrypted at rest and the production rules apply.
    /// </param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCodigoActivo(
        this IServiceCollection services,
        IConfiguration configuration,
        bool production = false
    )
    {
        if (production)
        {
            ProductionConfiguration.Validate(configuration);
        }

        return services
            .AddPlatform(configuration, production)
            .AddLegal(configuration)
            .AddPersistence(configuration)
            .AddLogs(configuration)
            .AddAccounts(configuration)
            .AddUsers()
            .AddEvents()
            .AddContent()
            .AddEmails(configuration)
            .AddUseCases();
    }
}
