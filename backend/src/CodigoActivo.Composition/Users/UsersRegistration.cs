using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Messaging;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Users;

/// <summary>
/// Registers users: households, who may act for whom and the scheduled erasure of accounts nobody
/// verified and of dependents who came of age.
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
        services.AddScoped<ActingUserPolicy>();
        services.AddSingleton(
            ScheduledJob.Of<PurgeUnverifiedAccountsCommand>(TimeSpan.FromHours(1))
        );
        services.AddSingleton(
            ScheduledJob.Of<PurgeAdultDependentsCommand, SendComingOfAgeNoticesCommand>(
                TimeSpan.FromHours(1)
            )
        );
        return services;
    }
}
