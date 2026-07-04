using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Security;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Seeders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API through <see cref="WebApplicationFactory{TEntryPoint}"/> with three swaps that
/// keep the pipeline faithful but the tests fast and deterministic:
/// <list type="bullet">
///   <item>PostgreSQL/Npgsql → EF Core in-memory (a fresh, uniquely-named store per factory).</item>
///   <item>Argon2id hasher → <see cref="FakePasswordHasher"/>.</item>
///   <item>System clock → a fixed <see cref="TestClock"/>.</item>
/// </list>
/// Everything else — cookie auth, CSRF middleware, controllers, services, repositories — is the
/// production wiring. The host runs under the Development environment so the session and antiforgery
/// cookies are not marked <c>Secure</c> and therefore flow over the http test server.
/// </summary>
public sealed class CodigoActivoWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = "codigoactivo-tests-" + Guid.NewGuid().ToString("N");

    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // We own the schema and seeding for the in-memory store.
                    ["Database:MigrateOnStartup"] = "false",
                    ["Database:SeedOnStartup"] = "false",
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=unused",
                    ["Cors:AllowedOrigins:0"] = "http://localhost",
                    ["Auth:SameSite"] = "Lax",
                }
            )
        );

        builder.ConfigureTestServices(services =>
        {
            ReplaceDbContext(services);

            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher, FakePasswordHasher>();

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    private void ReplaceDbContext(IServiceCollection services)
    {
        // Drop every descriptor tied to the Npgsql context (options, the EF 9+
        // IDbContextOptionsConfiguration, and the context itself) without naming EF-version-specific
        // types, then register the in-memory provider.
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(CodigoActivoDbContext)
                || (d.ServiceType.FullName?.Contains("DbContextOptions", StringComparison.Ordinal) ?? false)
            )
            .ToList();
        foreach (var descriptor in toRemove) services.Remove(descriptor);

        services.AddDbContext<CodigoActivoDbContext>(options =>
            options
                .UseInMemoryDatabase(databaseName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        );
    }

    /// <summary>Wipes and reseeds reference data + the fixed test users. Called before every test.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<CodigoActivoDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        await provider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        await TestSeedData.SeedUsersAsync(db);
    }

    /// <summary>Adds domain data in a scoped unit of work; <c>SaveChanges</c> is called for you.</summary>
    public async Task SeedAsync(Func<CodigoActivoDbContext, Task> seed)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    /// <summary>Reads from the store on a fresh scope (no tracking bleed from the request pipeline).</summary>
    public async Task<T> QueryAsync<T>(Func<CodigoActivoDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        return await query(db);
    }
}
