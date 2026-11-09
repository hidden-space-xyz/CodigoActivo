using CodigoActivo.API.Errors;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EndpointProtectionTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Unknown = "5d1e7c3a-2b4f-4e6a-9c8d-0f1a2b3c4d5e";

    public static TheoryData<string, string> SignedInOnly =>
        new()
        {
            { "GET", "/api/auth/me" },
            { "POST", "/api/auth/two-factor/authenticator/setup" },
            { "GET", "/api/activities/signup-roles" },
            { "GET", $"/api/activities/household-assignments/{Unknown}" },
            { "GET", $"/api/events/{Unknown}/terms" },
            { "GET", $"/api/events/{Unknown}/leader-roster" },
            { "PUT", $"/api/events/{Unknown}/rating" },
            { "DELETE", $"/api/events/{Unknown}/rating" },
            { "GET", "/api/me/assigned-activities" },
            { "GET", "/api/me/certificates" },
            { "GET", "/api/me/event-history" },
            { "GET", "/api/me/personal-data" },
            { "PUT", $"/api/users/{Unknown}" },
            { "GET", "/api/legal-policies/Privacy/versions" },
            { "GET", "/api/legal-policies/Privacy/versions/1" },
        };

    public static TheoryData<string, string> StaffOnly =>
        new()
        {
            { "POST", "/api/events" },
            { "GET", $"/api/events/{Unknown}/ratings" },
            { "POST", $"/api/activities/{Unknown}" },
            { "POST", "/api/news" },
            { "POST", "/api/partners" },
            { "POST", "/api/resources" },
            { "GET", "/api/resources/types" },
            { "GET", "/api/users/types" },
            { "POST", $"/api/users/{Unknown}/two-factor/reset" },
            { "GET", "/api/emails/users/audience" },
            { "GET", $"/api/emails/events/{Unknown}/attendees/audience" },
            { "GET", "/api/reports/dashboard" },
            { "GET", $"/api/reports/events/{Unknown}/attendees" },
            { "GET", $"/api/reports/events/{Unknown}/badges" },
            { "GET", $"/api/reports/events/{Unknown}/roster" },
            { "PUT", "/api/legal-policies/Privacy" },
            { "GET", "/api/blocked-data/consultations" },
        };

    public static TheoryData<string, string, string, ErrorCode> UnknownTargets =>
        new()
        {
            { "anonymous", "GET", $"/api/events/{Unknown}", ErrorCode.EventNotFound },
            { "anonymous", "GET", $"/api/activities/{Unknown}", ErrorCode.ActivityNotFound },
            { "anonymous", "GET", $"/api/partners/{Unknown}", ErrorCode.PartnerNotFound },
            { "anonymous", "GET", $"/api/resources/{Unknown}", ErrorCode.ResourceNotFound },
            { "anonymous", "GET", $"/api/files/{Unknown}/content", ErrorCode.FileNotFound },
            { "member", "GET", $"/api/events/{Unknown}/terms", ErrorCode.EventNotFound },
            { "member", "GET", $"/api/events/{Unknown}/leader-roster", ErrorCode.EventNotFound },
            {
                "member",
                "GET",
                $"/api/activities/household-assignments/{Unknown}",
                ErrorCode.EventNotFound
            },
            { "admin", "GET", $"/api/users/{Unknown}", ErrorCode.UserNotFound },
            { "admin", "GET", $"/api/events/{Unknown}/ratings", ErrorCode.EventNotFound },
            { "admin", "PATCH", $"/api/events/{Unknown}/feature", ErrorCode.EventNotFound },
            { "admin", "PATCH", $"/api/news/{Unknown}/feature", ErrorCode.NewsItemNotFound },
            {
                "admin",
                "DELETE",
                $"/api/events/categoryType/{Unknown}",
                ErrorCode.EventCategoryTypeNotFound
            },
            {
                "admin",
                "GET",
                $"/api/emails/events/{Unknown}/attendees/audience",
                ErrorCode.EventNotFound
            },
            { "admin", "GET", $"/api/reports/events/{Unknown}/attendees", ErrorCode.EventNotFound },
            { "admin", "GET", $"/api/reports/events/{Unknown}/badges", ErrorCode.EventNotFound },
            { "admin", "GET", $"/api/reports/events/{Unknown}/roster", ErrorCode.EventNotFound },
            { "admin", "GET", $"/api/reports/events/{Unknown}/summary", ErrorCode.EventNotFound },
        };

    public static TheoryData<string> CsrfGuarded =>
        [
            "/api/auth/login",
            "/api/events",
            $"/api/activities/{Unknown}",
            "/api/news",
            "/api/partners",
            "/api/resources",
            "/api/files",
        ];

    [Theory]
    [MemberData(nameof(SignedInOnly))]
    [MemberData(nameof(StaffOnly))]
    public async Task AnonymousRequestIsUnauthorized(string method, string url)
    {
        using var response = await CreateClient()
            .SendWithCsrfAsync(new HttpMethod(method), url, ct: Ct);

        await response.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
    }

    [Theory]
    [MemberData(nameof(StaffOnly))]
    public async Task MemberRequestIsForbidden(string method, string url)
    {
        var client = await LoginAsMemberAsync();

        using var response = await client.SendWithCsrfAsync(new HttpMethod(method), url, ct: Ct);

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
    }

    [Theory]
    [MemberData(nameof(UnknownTargets))]
    public async Task UnknownTargetIsNotFoundWithItsCode(
        string caller,
        string method,
        string url,
        ErrorCode code
    )
    {
        var client = caller switch
        {
            "admin" => await LoginAsAdminAsync(),
            "member" => await LoginAsMemberAsync(),
            _ => CreateClient(),
        };

        using var response = await client.SendWithCsrfAsync(new HttpMethod(method), url, ct: Ct);

        await response.ShouldBeNotFoundAsync(code);
    }

    [Theory]
    [MemberData(nameof(CsrfGuarded))]
    public async Task UnsafeRequestWithoutCsrfTokenIsRejected(string url)
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.PostAsync(TestUri.Rel(url), content: null, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);
    }
}
