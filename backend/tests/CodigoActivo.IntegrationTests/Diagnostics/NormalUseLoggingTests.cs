using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.API.News.Contracts;
using CodigoActivo.Application.Files.Contracts;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Diagnostics;

public sealed class NormalUseLoggingTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static async Task GetOkAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(TestUri.Rel(url), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, url);
    }

    private static async Task LogoutAsync(HttpClient client)
    {
        using var response = await client.PostJsonAsync("/api/auth/logout", body: null, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task BrowseAnonymouslyAsync(HttpClient client)
    {
        foreach (
            var url in new[]
            {
                "/api/news",
                "/api/events",
                "/api/partners",
                "/api/resources",
                "/api/robots.txt",
                "/api/sitemap.xml",
            }
        )
        {
            await GetOkAsync(client, url);
        }
    }

    private async Task UseAsMemberWithAStaleCsrfTokenAsync()
    {
        var client = CreateClient();
        var anonymousToken = await client.FetchCsrfTokenAsync(Ct);
        var credentials = TestSeedData.MemberCredentials;
        await PassPasswordStepAsync(client, credentials);
        await CompleteTwoFactorAsync(
            client,
            Factory.EmailSender.LastLoginCodeSentTo(credentials.Identifier)
        );

        await GetOkAsync(client, "/api/auth/me");
        await GetOkAsync(client, "/api/me/assigned-activities");
        await GetOkAsync(client, "/api/me/event-history");

        using var stale = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        stale.Headers.Add("X-CSRF-TOKEN", anonymousToken);
        using var rejected = await client.SendAsync(stale, Ct);
        await rejected.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);

        await LogoutAsync(client);
    }

    private async Task UseAsAdminAsync()
    {
        var client = await LoginAsAdminAsync();

        using var upload = await client.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            TestImages.Png()
        );
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var thumbnail = await upload.ReadJsonAsync<FileResponse>(Ct);

        using var created = await client.PostJsonAsync(
            "/api/news",
            new CreateNewsItemRequest("Asamblea", "Sábado", "{}", thumbnail!.Id),
            Ct
        );
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        await GetOkAsync(client, "/api/users");
        await GetOkAsync(client, "/api/reports/dashboard");

        using var email = await client.SendEmailFormAsync("/api/emails/users");
        email.StatusCode.Should().Be(HttpStatusCode.OK);

        await LogoutAsync(client);
    }

    [Fact]
    public async Task NormalUseLeavesNothingInTheLog()
    {
        await BrowseAnonymouslyAsync(CreateClient());
        await UseAsMemberWithAStaleCsrfTokenAsync();
        await UseAsAdminAsync();

        Factory.Logs.Entries.Should().BeEmpty();
    }
}
