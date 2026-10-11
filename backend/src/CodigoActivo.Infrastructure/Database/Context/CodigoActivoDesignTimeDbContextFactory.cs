using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CodigoActivo.Infrastructure.Database.Context;

/// <summary>
/// Builds the write context for the EF Core tools, so migrations are created from Infrastructure
/// alone and the design-time packages never ship with the API. Creating a migration never opens
/// the connection, so it points at a local placeholder database.
/// </summary>
public sealed class CodigoActivoDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<CodigoActivoDbContext>
{
    private const string PlaceholderConnection = "Host=design-time.invalid;Database=codigoactivo";

    /// <inheritdoc />
    public CodigoActivoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CodigoActivoDbContext>();
        options.UseWriteDatabase(PlaceholderConnection);
        return new CodigoActivoDbContext(options.Options);
    }
}
