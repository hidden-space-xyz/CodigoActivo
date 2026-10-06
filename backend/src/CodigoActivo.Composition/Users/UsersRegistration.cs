using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Users;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.Users;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Users;

/// <summary>
/// Registers users: households, who may act for whom and the erasure of accounts.
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
        services.AddScoped<IAccountErasureStore, AccountErasureStore>();
        services.AddScoped<ActingUserPolicy>();
        services.AddScoped<AccountEraser>();
        services.AddValidatedOptions<DeletedAccountPurgeOptions>(_ => { });
        services.AddHostedService<DeletedAccountPurger>();
        return services;
    }
}
