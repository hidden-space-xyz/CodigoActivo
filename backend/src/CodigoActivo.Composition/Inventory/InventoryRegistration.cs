using CodigoActivo.Domain.Inventory;
using CodigoActivo.Infrastructure.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Composition.Inventory;

/// <summary>
/// Registers the inventory the association keeps of its belongings.
/// </summary>
internal static class InventoryRegistration
{
    /// <summary>
    /// Adds the inventory category repository.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddInventory(this IServiceCollection services)
    {
        services.AddScoped<IInventoryCategoryRepository, InventoryCategoryRepository>();
        return services;
    }
}
