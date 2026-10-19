using CodigoActivo.Application.Common.Security;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Users;

/// <summary>
/// Registers users: households, who may act for whom, the legal copies of erased accounts and the
/// scheduled erasure of accounts nobody verified and of dependents who came of age.
/// </summary>
internal static class UsersRegistration
{
    /// <summary>
    /// Adds the user services.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddUsers(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeletedAccountRepository, DeletedAccountRepository>();
        services.AddScoped<ActingUserPolicy>();
        services.AddFixedOptions<DeletedAccountPurgeOptions>();
        services.AddHostedService<DeletedAccountPurgeWorker>();
        services.AddFixedOptions<UnverifiedAccountPurgeOptions>();
        services.AddHostedService<UnverifiedAccountPurgeWorker>();
        services.AddFixedOptions<AdultDependentPurgeOptions>();
        services.AddHostedService<AdultDependentPurgeWorker>();
        return services;
    }
}
