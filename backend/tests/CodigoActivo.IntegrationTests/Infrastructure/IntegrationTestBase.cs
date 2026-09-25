using System.Net;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
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
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected HttpClient CreateClient()
    {
        return Factory.CreateClient();
    }

    protected Task<HttpClient> LoginAsync(TestCredentials credentials, bool keepSignedIn = false)
    {
        return LoginAsync(Factory, credentials, keepSignedIn);
    }

    /// <summary>
    /// Signs in completely: the password step followed by the emailed second factor, which every
    /// seeded user relies on. The login-code email is removed from the recorder afterwards so the
    /// test only sees the mail its own scenario produces.
    /// </summary>
    protected async Task<HttpClient> LoginAsync(
        WebApplicationFactory<Program> host,
        TestCredentials credentials,
        bool keepSignedIn = false
    )
    {
        var client = host.CreateClient();
        await PassPasswordStepAsync(client, credentials);
        var code = Factory.EmailSender.LastLoginCodeSentTo(credentials.Identifier);
        await CompleteTwoFactorAsync(client, code, keepSignedIn);
        Factory.EmailSender.ForgetLoginCodes();
        return client;
    }

    /// <summary>
    /// Runs only the password step, leaving the client with a pending second-factor challenge.
    /// </summary>
    protected static async Task<LoginChallengeResponse> PassPasswordStepAsync(
        HttpClient client,
        TestCredentials credentials
    )
    {
        using var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(credentials.Identifier, credentials.Password),
            Ct
        );
        if (response.StatusCode is not HttpStatusCode.OK)
        {
            var status = $"{response.StatusCode:D}";
            throw new InvalidOperationException(
                $"Test login failed for '{credentials.Identifier}' with status {status}."
            );
        }

        return (await response.ReadJsonAsync<LoginChallengeResponse>(Ct))!;
    }

    /// <summary>
    /// Presents the second factor for the pending challenge of the client.
    /// </summary>
    protected static async Task<UserResponse> CompleteTwoFactorAsync(
        HttpClient client,
        string code,
        bool keepSignedIn = false
    )
    {
        using var response = await client.PostJsonAsync(
            "/api/auth/login/two-factor",
            new TwoFactorLoginRequest(code, keepSignedIn),
            Ct
        );
        if (response.StatusCode is not HttpStatusCode.OK)
        {
            var status = $"{response.StatusCode:D}";
            throw new InvalidOperationException(
                $"Test second-factor step failed with status {status}."
            );
        }

        return (await response.ReadJsonAsync<UserResponse>(Ct))!;
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
