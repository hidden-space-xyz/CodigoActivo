using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Legal.Contracts;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class LegalPoliciesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static string Text(string text)
    {
        return $$$"""{"type":"doc","content":[{"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Título"}]},{"type":"paragraph","content":[{"type":"text","text":"{{{text}}}","marks":[{"type":"link","attrs":{"href":"/cookies"}}]}]}]}""";
    }

    private static async Task<LegalPolicyResponse> PublishAsync(
        HttpClient admin,
        string kind,
        string content
    )
    {
        using var response = await admin.PutJsonAsync(
            $"/api/legal-policies/{kind}",
            new PublishLegalPolicyRequest(content),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.ReadJsonAsync<LegalPolicyResponse>(Ct))!;
    }

    [Fact]
    public async Task GetUnpublishedPolicyReturnsNotFound()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel("/api/legal-policies/Privacy"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.LegalPolicyNotFound);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsAdminServesTheVersionInForceToAnyone()
    {
        var admin = await LoginAsAdminAsync();

        var published = await PublishAsync(admin, "Privacy", Text("Primera"));

        published.Kind.Should().Be(LegalPolicyKind.Privacy);
        published.Version.Should().Be(1);
        using var response = await CreateClient()
            .GetAsync(TestUri.Rel("/api/legal-policies/privacy"), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = await response.ReadJsonAsync<LegalPolicyResponse>(Ct);
        policy.Should().Be(published);
        using var cookies = await CreateClient()
            .GetAsync(TestUri.Rel("/api/legal-policies/Cookies"), Ct);
        await cookies.ShouldBeNotFoundAsync(ErrorCode.LegalPolicyNotFound);
    }

    [Fact]
    public async Task PublishTwiceKeepsEveryVersionForAdministrators()
    {
        var admin = await LoginAsAdminAsync();
        await PublishAsync(admin, "Cookies", Text("Primera"));
        var unchanged = await PublishAsync(admin, "Cookies", Text("Primera"));
        var revised = await PublishAsync(admin, "Cookies", Text("Segunda"));

        unchanged.Version.Should().Be(1);
        revised.Version.Should().Be(2);
        using var versions = await admin.GetAsync(
            TestUri.Rel("/api/legal-policies/Cookies/versions"),
            Ct
        );
        versions.StatusCode.Should().Be(HttpStatusCode.OK);
        (await versions.ReadJsonAsync<List<LegalPolicyVersionResponse>>(Ct))!
            .Select(version => version.Version)
            .Should()
            .Equal(2, 1);
        using var first = await admin.GetAsync(
            TestUri.Rel("/api/legal-policies/Cookies/versions/1"),
            Ct
        );
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstVersion = await first.ReadJsonAsync<LegalPolicyResponse>(Ct);
        firstVersion!.Version.Should().Be(1);
        firstVersion.Content.Should().Contain("Primera");
        using var missing = await admin.GetAsync(
            TestUri.Rel("/api/legal-policies/Cookies/versions/3"),
            Ct
        );
        await missing.ShouldBeNotFoundAsync(ErrorCode.LegalPolicyVersionNotFound);
    }

    [Fact]
    public async Task ListVersionsOfUnpublishedPolicyReturnsNotFound()
    {
        var admin = await LoginAsAdminAsync();

        using var response = await admin.GetAsync(
            TestUri.Rel("/api/legal-policies/Privacy/versions"),
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.LegalPolicyNotFound);
    }

    [Fact]
    public async Task PublishWithoutTextIsRefused()
    {
        var admin = await LoginAsAdminAsync();

        using var response = await admin.PutJsonAsync(
            "/api/legal-policies/Privacy",
            new PublishLegalPolicyRequest("""{"type":"doc","content":[{"type":"paragraph"}]}"""),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.LegalPolicyContentRequired);
    }

    [Fact]
    public async Task PublishWithAnImageIsRefused()
    {
        var admin = await LoginAsAdminAsync();
        var image =
            $$$"""{"type":"doc","content":[{"type":"image","attrs":{"src":"/api/files/{{{Guid.NewGuid()}}}/content"}}]}""";

        using var response = await admin.PutJsonAsync(
            "/api/legal-policies/Privacy",
            new PublishLegalPolicyRequest(image),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Theory]
    [InlineData("Terms")]
    [InlineData("7")]
    public async Task GetUndefinedKindIsRejected(string kind)
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel($"/api/legal-policies/{kind}"), Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task PublishAsMemberIsForbidden()
    {
        var member = await LoginAsMemberAsync();

        using var response = await member.PutJsonAsync(
            "/api/legal-policies/Privacy",
            new PublishLegalPolicyRequest(Text("Texto")),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
    }

    [Theory]
    [InlineData("/api/legal-policies/Privacy/versions")]
    [InlineData("/api/legal-policies/Privacy/versions/1")]
    public async Task VersionsAnonymouslyRequireAuthentication(string url)
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel(url), Ct);

        await response.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
    }
}
