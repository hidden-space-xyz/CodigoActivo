using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class PartnersControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task<Guid> SeedPartnerAsync(string name = "Existing", DateOnly? fromDate = null)
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
                    FromDate = fromDate ?? new DateOnly(2024, 1, 1),
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
    public async Task ListAnonymousReturnsPagedEnvelope()
    {
        await SeedPartnerAsync("Alpha");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/partners"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<PartnerResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.Items.Should().ContainSingle(p => p.Name == "Alpha");
    }

    [Fact]
    public async Task ListFilterByFromDateRangeAppliesInclusiveBounds()
    {
        await SeedPartnerAsync("Antiguo", new DateOnly(2024, 1, 1));
        await SeedPartnerAsync("Medio", new DateOnly(2024, 6, 15));
        await SeedPartnerAsync("Reciente", new DateOnly(2024, 12, 31));
        await SeedPartnerAsync("Fuera", new DateOnly(2025, 1, 1));
        var client = CreateClient();

        var response = await client.GetAsync(
            TestUri.Rel("/api/partners?fromDateFrom=2024-06-15&fromDateTo=2024-12-31"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<PartnerResponse>>(Ct);
        page!.Total.Should().Be(2);
        page.Items.Select(p => p.Name).Should().BeEquivalentTo("Medio", "Reciente");
    }

    [Fact]
    public async Task GetPartnerAbsentReturns404WithErrorCode()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/partners/{Guid.NewGuid()}"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.PartnerNotFound);
    }

    [Fact]
    public async Task CreateAdminPersistsAndReturns201WithLocation()
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

        var response = await client.PostJsonAsync("/api/partners", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<PartnerResponse>(Ct);
        created!.Name.Should().Be("Gamma");

        var stored = await FindAsync<Partner>(created.Id);
        stored!.Tier.Should().Be(3);
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task CreateMemberReturnsForbidden()
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

        var response = await client.PostJsonAsync("/api/partners", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();
        var request = new CreatePartnerRequest(
            "Nope",
            new DateOnly(2025, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var response = await client.PostJsonAsync("/api/partners", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBlankNameReturnsValidationError()
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

        var response = await client.PostJsonAsync("/api/partners", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task CreateNullFromDateReturnsValidationError()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreatePartnerRequest("Sin fecha", null, 1, null, thumbnailId);

        var response = await client.PostJsonAsync("/api/partners", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task CreateMissingCsrfTokenIsRejected()
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

        var response = await client.SendAsync(request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task UpdateAdminChangesPartner()
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

        var response = await client.PutJsonAsync($"/api/partners/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<Partner>(id);
        stored!.Name.Should().Be("After");
        stored.Tier.Should().Be(4);
    }

    [Fact]
    public async Task DeleteAdminRemovesPartnerAndOrphanedThumbnail()
    {
        var id = await SeedPartnerAsync("Doomed");
        var thumbnailId = (await FindAsync<Partner>(id))!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/partners/{id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<Partner>(id);
        stored.Should().BeNull();
        var file = await FindAsync<FileEntity>(thumbnailId);
        file.Should()
            .BeNull("the deleted partner's thumbnail is orphaned and must be cascade-deleted");
    }
}
