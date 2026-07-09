using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class PartnersControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task<Guid> SeedThumbnailAsync()
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

    private async Task<Guid> SeedPartnerAsync(string name = "Existing")
    {
        var thumbnailId = await SeedThumbnailAsync();
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Partners.Add(
                new Partner
                {
                    Id = id,
                    Name = name,
                    Tier = 1,
                    FromDate = new DateOnly(2024, 1, 1),
                    ThumbnailId = thumbnailId,
                    CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    [Fact]
    public async Task List_is_anonymous_and_returns_paged_envelope()
    {
        await SeedPartnerAsync("Alpha");
        var client = CreateClient();

        var response = await client.GetAsync("/api/partners");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<PartnerResponse>>();
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.Items.Should().ContainSingle(p => p.Name == "Alpha");
    }

    [Fact]
    public async Task Get_returns_404_with_error_code_when_absent()
    {
        var client = CreateClient();

        var response = await client.GetAsync($"/api/partners/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.PartnerNotFound);
    }

    [Fact]
    public async Task Create_as_admin_persists_and_returns_201_with_location()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreatePartnerRequest(
            "Gamma",
            new DateOnly(2025, 4, 1),
            3,
            "https://gamma.test",
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/partners", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<PartnerResponse>();
        created!.Name.Should().Be("Gamma");

        var stored = await Factory.QueryAsync(db => db.Partners.FindAsync(created.Id).AsTask());
        stored!.Tier.Should().Be(3);
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Create_as_member_is_forbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsMemberAsync();
        var request = new CreatePartnerRequest(
            "Nope",
            new DateOnly(2025, 1, 1),
            1,
            null,
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/partners", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_anonymous_is_unauthorized()
    {
        var client = CreateClient();
        var request = new CreatePartnerRequest(
            "Nope",
            new DateOnly(2025, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var response = await client.PostJsonAsync("/api/partners", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_with_blank_name_is_validation_error()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreatePartnerRequest(
            "   ",
            new DateOnly(2025, 1, 1),
            1,
            null,
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/partners", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Post_without_csrf_token_is_rejected()
    {
        var client = await LoginAsAdminAsync();
        var thumbnailId = await SeedThumbnailAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/partners")
        {
            Content = JsonContent.Create(
                new CreatePartnerRequest("Gamma", new DateOnly(2025, 1, 1), 1, null, thumbnailId),
                options: TestJson.Options
            ),
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Update_as_admin_changes_partner()
    {
        var id = await SeedPartnerAsync("Before");
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdatePartnerRequest(
            "After",
            new DateOnly(2025, 6, 6),
            4,
            "https://after.test",
            thumbnailId
        );

        var response = await client.PutJsonAsync($"/api/partners/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db => db.Partners.FindAsync(id).AsTask());
        stored!.Name.Should().Be("After");
        stored.Tier.Should().Be(4);
    }

    [Fact]
    public async Task Delete_as_admin_removes_partner_and_its_orphaned_thumbnail()
    {
        var id = await SeedPartnerAsync("Doomed");
        var thumbnailId = (
            await Factory.QueryAsync(db => db.Partners.FindAsync(id).AsTask())
        )!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/partners/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Partners.FindAsync(id).AsTask());
        stored.Should().BeNull();
        var file = await Factory.QueryAsync(db => db.Files.FindAsync(thumbnailId).AsTask());
        file.Should()
            .BeNull("the deleted partner's thumbnail is orphaned and must be cascade-deleted");
    }
}
