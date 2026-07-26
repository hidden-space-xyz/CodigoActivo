using System.Net;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;
using Xunit;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase(CodigoActivoWebAppFactory factory)
    : IClassFixture<CodigoActivoWebAppFactory>,
        IAsyncLifetime
{
    protected CodigoActivoWebAppFactory Factory { get; } = factory;

    protected static CancellationToken Ct => TestCancellation.Ct;

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
        return response.StatusCode != HttpStatusCode.OK
            ? throw new InvalidOperationException(
                $"Test login failed for '{credentials.Identifier}' with status {(int)response.StatusCode}."
            )
            : client;
    }

    protected Task<HttpClient> LoginAsAdminAsync()
    {
        return LoginAsync(TestSeedData.AdminCredentials);
    }

    protected Task<HttpClient> LoginAsMemberAsync()
    {
        return LoginAsync(TestSeedData.MemberCredentials);
    }

    protected async Task<Guid> SeedThumbnailAsync()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = id,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    protected Task<T?> FindAsync<T>(Guid id)
        where T : class
    {
        return Factory.QueryAsync(db => db.Set<T>().FindAsync([id], Ct).AsTask());
    }
}
