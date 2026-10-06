using CodigoActivo.API.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class UnknownRouteTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("/api/whatever")]
    [InlineData("/api/events/not-a-guid/whatever")]
    public async Task UnknownApiRouteAnonymousReturnsNotFound(string path)
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel(path), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.EndpointNotFound);
    }

    [Fact]
    public async Task UnknownApiRouteSignedInReturnsNotFound()
    {
        var client = await LoginAsMemberAsync();

        using var response = await client.GetAsync(TestUri.Rel("/api/whatever"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.EndpointNotFound);
    }

    [Fact]
    public async Task KnownProtectedRouteAnonymousStillRequiresAuthentication()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel("/api/users"), Ct);

        await response.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
    }
}
