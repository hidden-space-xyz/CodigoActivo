using System.Net;
using System.Net.Http.Json;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CodigoActivo.IntegrationTests;

/// <summary>
/// Validates the integration toolchain end-to-end: the real host boots on the in-memory store, the
/// database seeds, cookie login works, CSRF is enforced, and the Result→HTTP error mapping fires.
/// </summary>
public class SmokeTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Csrf_endpoint_returns_a_token()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/csrf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<CsrfTokenResponse>();
        body!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Admin_can_log_in_and_read_its_own_profile()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await response.ReadJsonAsync<UserResponse>();
        me!.Email.Should().Be(TestSeedData.AdminEmail);
        me.Roles.Should().Contain(role => role.Id == SeedIds.UserTypes.Admin);
    }

    [Fact]
    public async Task Anonymous_request_to_protected_endpoint_is_401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unsafe_request_without_csrf_token_is_rejected()
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(
                new LoginRequest(TestSeedData.AdminEmail, TestSeedData.Password),
                options: TestJson.Options
            ),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<CodigoActivo.API.Extensions.ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }
}
