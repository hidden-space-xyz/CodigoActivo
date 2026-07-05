using System.Net;
using CodigoActivo.Application.DTOs;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

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

    protected HttpClient CreateClient()
    {
        return Factory.CreateClient();
    }

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
