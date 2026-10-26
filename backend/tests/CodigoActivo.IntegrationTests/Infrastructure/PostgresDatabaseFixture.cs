using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Gives a test class a database of its own, copied from the migrated template of
/// <see cref="PostgresServerFixture"/>, and drops it once the class finishes.
/// </summary>
public sealed class PostgresDatabaseFixture(PostgresServerFixture server) : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await server.CreateDatabaseAsync();
    }

    public CodigoActivoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoDbContext>();
        options.UseWriteDatabase(ConnectionString);
        return new CodigoActivoDbContext(options.Options);
    }

    public CodigoActivoReadDbContext CreateReadContext()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoReadDbContext>();
        options.UseReadDatabase(ConnectionString);
        return new CodigoActivoReadDbContext(options.Options);
    }

    public async ValueTask DisposeAsync()
    {
        if (ConnectionString.Length is not 0)
        {
            await server.DropDatabaseAsync(ConnectionString);
        }
    }
}
