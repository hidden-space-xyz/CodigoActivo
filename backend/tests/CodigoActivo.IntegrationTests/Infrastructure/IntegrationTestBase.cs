using System.Net;
using CodigoActivo.Application.DTOs;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Base for HTTP-level integration tests. Each test starts from a freshly reseeded in-memory store
/// (reference data + the fixed <see cref="TestSeedData"/> users). Exposes cookie-aware clients and
/// login helpers; every test class gets its own factory (hence its own isolated database).
/// Parallelization is disabled assembly-wide (see <c>TestParallelization.cs</c>) so the multiple
/// hosts never race on Serilog's static logger or its shared log file.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<CodigoActivoWebAppFactory>, IAsyncLifetime
{
    protected IntegrationTestBase(CodigoActivoWebAppFactory factory)
    {
        Factory = factory;
    }

    protected CodigoActivoWebAppFactory Factory { get; }

    public async ValueTask InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>An anonymous, cookie-aware client (no session).</summary>
    protected HttpClient CreateClient()
    {
        return Factory.CreateClient();
    }

    /// <summary>Logs the given seeded user in and returns a client carrying the session cookie.</summary>
    protected async Task<HttpClient> LoginAsync(TestCredentials credentials)
    {
        var client = Factory.CreateClient();
        using var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(credentials.Identifier, credentials.Password)
        );
        if (response.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException(
                $"Test login failed for '{credentials.Identifier}' with status {(int)response.StatusCode}."
            );

        return client;
    }

    protected Task<HttpClient> LoginAsAdminAsync()
    {
        return LoginAsync(TestSeedData.AdminCredentials);
    }

    protected Task<HttpClient> LoginAsMemberAsync()
    {
        return LoginAsync(TestSeedData.MemberCredentials);
    }
}
