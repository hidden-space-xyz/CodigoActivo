using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Constants;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests;

public class SmokeTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Me_AdminLoggedIn_ReturnsOwnProfile()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await response.ReadJsonAsync<UserResponse>(TestContext.Current.CancellationToken);
        me!.Email.Should().Be(TestSeedData.AdminEmail);
        me.IsAdmin.Should().BeTrue();
    }
}
