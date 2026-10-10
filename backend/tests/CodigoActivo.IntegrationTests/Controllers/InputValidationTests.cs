using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class InputValidationTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("/api/auth/forgot-password")]
    [InlineData("/api/auth/resend-verification")]
    public async Task AnonymousBodyWithNullCharacterReturnsBadRequest(string url)
    {
        var client = CreateClient();

        using var response = await client.PostJsonAsync(
            url,
            new { email = "ana\u0000@example.com" },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task LoginWithControlCharacterInPasswordReturnsBadRequest()
    {
        var client = CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/auth/login",
            new { identifier = "ana@example.com", password = "Contraseña\u001B[31m" },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Theory]
    [InlineData("/api/events?search=%00")]
    [InlineData("/api/news?title=a%0Ab")]
    [InlineData("/api/resources?search=%1B")]
    [InlineData("/api/partners?name=%00")]
    [InlineData("/api/activities?title=%00")]
    [InlineData("/api/events?sort=title%00")]
    public async Task ListCriterionWithControlCharacterReturnsBadRequest(string url)
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel(url), Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ListCriterionLongerThanTheLimitReturnsBadRequest()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(
            TestUri.Rel($"/api/events?search={new string('a', 201)}"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Theory]
    [InlineData("/api/events?signupTo=9999-12-31")]
    [InlineData("/api/events?eventDateTo=9999-12-31")]
    [InlineData("/api/news?createdTo=9999-12-31")]
    [InlineData("/api/resources?createdTo=9999-12-31")]
    [InlineData("/api/activities?activityDateTo=9999-12-31")]
    public async Task ListLastRepresentableDayFilterReturnsOk(string url)
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel(url), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("from=1999-12-31")]
    [InlineData("to=2100-01-01")]
    [InlineData("from=0001-01-01&to=9999-12-31")]
    public async Task DashboardAnalyticsOutsideTheAllowedDaysReturnsBadRequest(string range)
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.GetAsync(
            TestUri.Rel($"/api/reports/dashboard/analytics?{range}"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task SignupWithMoreTermsDecisionsThanTheLimitReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();
        var decisions = Enumerable
            .Range(0, 51)
            .Select(_ => new { termsDocumentId = Guid.NewGuid(), accepted = true })
            .ToArray();

        using var response = await client.PatchJsonAsync(
            $"/api/activities/{Guid.NewGuid()}/{TestSeedData.Users.MemberId}/assign",
            new { activityRoleTypeId = Guid.NewGuid(), termsDecisions = decisions },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }
}
