using System.Net;
using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EnumBindingTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static object RegisterPayload(object gender)
    {
        return new
        {
            firstName = "Nadia",
            lastName = "Nueva",
            email = "enum.binding@codigoactivo.test",
            phone = "+34600000098",
            password = "Str0ngPass!23",
            nationalId = "87654321X",
            gender,
        };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public async Task RegisterIntegerGenderInTheBodyIsRejected(int gender)
    {
        var client = CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/auth/register",
            RegisterPayload(gender),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task RegisterUndefinedGenderNameInTheBodyIsRejected()
    {
        var client = CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/auth/register",
            RegisterPayload("Unknown"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task RegisterGenderNameInTheBodyIsStillAccepted()
    {
        var client = CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/auth/register",
            RegisterPayload("Female"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("-1")]
    [InlineData("Sideways")]
    public async Task ListEventsUndefinedScopeInTheQueryStringIsRejected(string scope)
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel($"/api/events?scope={scope}"), Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ListEventsScopeNameInTheQueryStringIsStillAccepted()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel("/api/events?scope=Past"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EventAttendeesUndefinedGenderInTheQueryStringIsRejected()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.GetAsync(
            TestUri.Rel($"/api/reports/events/{Guid.NewGuid()}/attendees?gender=7"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task EventAttendeesGenderNameInTheQueryStringReachesTheHandler()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.GetAsync(
            TestUri.Rel($"/api/reports/events/{Guid.NewGuid()}/attendees?gender=Female"),
            Ct
        );

        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }
}
