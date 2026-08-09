using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class ResourcesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Description =
        "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Contenido\"}]}]}";
    private const string ExternalUrl = "https://ejemplo.es/curso";

    private async Task<Guid> SeedResourceAsync(
        string title = "Existing",
        string subtitle = "Sub",
        string? url = null,
        DateTimeOffset? createdAt = null
    )
    {
        var thumbnailId = await SeedThumbnailAsync();
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Resources.Add(
                new Resource
                {
                    Id = id,
                    Title = title,
                    Subtitle = subtitle,
                    Description = url is null ? Description : "{}",
                    Url = url,
                    ResourceTypeId = url is null
                        ? SeedIds.ResourceTypes.Internal
                        : SeedIds.ResourceTypes.External,
                    ThumbnailId = thumbnailId,
                    CreatedAt = createdAt ?? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    [Fact]
    public async Task ListAnonymousReturnsPagedEnvelopeWithType()
    {
        await SeedResourceAsync("Alpha");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/resources"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        var item = page.Items.Should().ContainSingle(r => r.Title == "Alpha").Subject;
        item.Type.Id.Should().Be(SeedIds.ResourceTypes.Internal);
        item.Type.Name.Should().Be("Interno");
        item.Type.IsExternal.Should().BeFalse();
        item.Url.Should().BeNull();
    }

    [Fact]
    public async Task ListFilterByResourceTypeIdReturnsOnlyMatchingType()
    {
        await SeedResourceAsync("Guia interna");
        var externalId = await SeedResourceAsync("Curso externo", url: ExternalUrl);
        var client = CreateClient();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/resources?resourceTypeId={SeedIds.ResourceTypes.External}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        var item = page.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(externalId);
        item.Type.Id.Should().Be(SeedIds.ResourceTypes.External);
    }

    [Fact]
    public async Task ListFilterByUrlMatchesAccentAndCaseInsensitively()
    {
        await SeedResourceAsync("Robotica", url: "https://ejemplo.es/robótica-avanzada");
        await SeedResourceAsync("Ajedrez", url: "https://ejemplo.es/ajedrez");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/resources?url=ROBOTICA"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(r => r.Title == "Robotica");
    }

    [Fact]
    public async Task ListFilterByCreatedRangeUsesAppTimezoneDayBounds()
    {
        Factory.Clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        await SeedResourceAsync(
            "DiaDiez",
            createdAt: new DateTimeOffset(2026, 3, 9, 22, 0, 0, TimeSpan.Zero)
        );
        await SeedResourceAsync(
            "DiaNueve",
            createdAt: new DateTimeOffset(2026, 3, 9, 21, 0, 0, TimeSpan.Zero)
        );
        var client = CreateClient();

        var fromResponse = await client.GetAsync(TestUri.Rel("/api/resources?createdFrom=2026-03-10"), Ct);
        var toResponse = await client.GetAsync(TestUri.Rel("/api/resources?createdTo=2026-03-09"), Ct);

        fromResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fromPage = await fromResponse.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(Ct);
        fromPage!.Total.Should().Be(1);
        fromPage.Items.Should().ContainSingle(r => r.Title == "DiaDiez");

        toResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var toPage = await toResponse.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(Ct);
        toPage!.Total.Should().Be(1);
        toPage.Items.Should().ContainSingle(r => r.Title == "DiaNueve");
    }

    [Fact]
    public async Task ListSortByTypeOrdersByTypeName()
    {
        await SeedResourceAsync(
            "Interno",
            createdAt: new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero)
        );
        await SeedResourceAsync(
            "Externo",
            url: ExternalUrl,
            createdAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/resources?sort=type"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(Ct);
        page!.Items.Select(r => r.Type.Name).Should().Equal("Externo", "Interno");
    }

    [Fact]
    public async Task ListSortByUrlOrdersByUrlWithNullsLast()
    {
        await SeedResourceAsync("SinUrl");
        await SeedResourceAsync("UrlB", url: "https://beta.test/recurso");
        await SeedResourceAsync("UrlA", url: "https://alfa.test/recurso");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/resources?sort=url"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(Ct);
        page!.Items.Select(r => r.Title).Should().Equal("UrlA", "UrlB", "SinUrl");
    }

    [Fact]
    public async Task TypesAdminReturnsSeededTypesOrderedByName()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/resources/types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<ResourceTypeResponse>>(Ct);
        types.Should().NotBeNull();
        types.Select(t => t.Name).Should().ContainInOrder("Externo", "Interno");
        types
            .Single(t => string.Equals(t.Name, "Externo", StringComparison.Ordinal))
            .IsExternal.Should()
            .BeTrue();
        types
            .Single(t => string.Equals(t.Name, "Interno", StringComparison.Ordinal))
            .IsExternal.Should()
            .BeFalse();
        types.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Color));
    }

    [Fact]
    public async Task TypesMemberReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/resources/types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetResourceAbsentReturns404WithErrorCode()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/resources/{Guid.NewGuid()}"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.ResourceNotFound);
    }

    [Fact]
    public async Task GetExternalResourceReturnsTypeAndUrl()
    {
        var id = await SeedResourceAsync("Enlace", url: ExternalUrl);
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/resources/{id}"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resource = await response.ReadJsonAsync<ResourceResponse>(Ct);
        resource!.Url.Should().Be(ExternalUrl);
        resource.Type.Id.Should().Be(SeedIds.ResourceTypes.External);
        resource.Type.IsExternal.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAdminPersistsAndReturns201WithLocation()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateResourceRequest(
            "Gamma",
            "Tagline",
            Description,
            null,
            SeedIds.ResourceTypes.Internal,
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/resources", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<ResourceResponse>(Ct);
        created!.Title.Should().Be("Gamma");
        created.Type.Id.Should().Be(SeedIds.ResourceTypes.Internal);

        var stored = await FindAsync<Resource>(created.Id);
        stored!.Subtitle.Should().Be("Tagline");
        stored.ResourceTypeId.Should().Be(SeedIds.ResourceTypes.Internal);
        stored.Url.Should().BeNull();
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task CreateExternalResourcePersistsUrlWithoutDescription()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateResourceRequest(
            "Curso externo",
            "Sub",
            null,
            ExternalUrl,
            SeedIds.ResourceTypes.External,
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/resources", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<ResourceResponse>(Ct);
        created!.Url.Should().Be(ExternalUrl);
        created.Type.IsExternal.Should().BeTrue();

        var stored = await FindAsync<Resource>(created.Id);
        stored!.Url.Should().Be(ExternalUrl);
        stored.Description.Should().Be("{}");
        stored.ResourceTypeId.Should().Be(SeedIds.ResourceTypes.External);
    }

    [Theory]
    [InlineData("internal-with-url")]
    [InlineData("external-with-description")]
    [InlineData("external-without-url")]
    [InlineData("internal-without-description")]
    [InlineData("unknown-type")]
    public async Task CreateTypeContentMismatchReturnsExpectedErrorCode(string scenario)
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var (request, expectedCode) = scenario switch
        {
            "internal-with-url" => (
                new CreateResourceRequest(
                    "Title",
                    "Sub",
                    Description,
                    ExternalUrl,
                    SeedIds.ResourceTypes.Internal,
                    thumbnailId
                ),
                ErrorCode.ResourceUrlNotAllowed
            ),
            "external-with-description" => (
                new CreateResourceRequest(
                    "Title",
                    "Sub",
                    Description,
                    ExternalUrl,
                    SeedIds.ResourceTypes.External,
                    thumbnailId
                ),
                ErrorCode.ResourceDescriptionNotAllowed
            ),
            "external-without-url" => (
                new CreateResourceRequest(
                    "Title",
                    "Sub",
                    null,
                    null,
                    SeedIds.ResourceTypes.External,
                    thumbnailId
                ),
                ErrorCode.ResourceUrlRequired
            ),
            "internal-without-description" => (
                new CreateResourceRequest(
                    "Title",
                    "Sub",
                    "{}",
                    null,
                    SeedIds.ResourceTypes.Internal,
                    thumbnailId
                ),
                ErrorCode.ResourceDescriptionRequired
            ),
            _ => (
                new CreateResourceRequest(
                    "Title",
                    "Sub",
                    Description,
                    null,
                    Guid.NewGuid(),
                    thumbnailId
                ),
                ErrorCode.ResourceTypeNotFound
            ),
        };

        var response = await client.PostJsonAsync("/api/resources", request, Ct);

        await response.ShouldBeBadRequestAsync(expectedCode);
    }

    [Fact]
    public async Task CreateMalformedUrlReturnsValidationError()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateResourceRequest(
            "Title",
            "Sub",
            null,
            "no-es-una-url",
            SeedIds.ResourceTypes.External,
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/resources", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task CreateMemberReturnsForbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsMemberAsync();
        var request = new CreateResourceRequest(
            "Nope",
            "Sub",
            Description,
            null,
            SeedIds.ResourceTypes.Internal,
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/resources", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();
        var request = new CreateResourceRequest(
            "Nope",
            "Sub",
            Description,
            null,
            SeedIds.ResourceTypes.Internal,
            Guid.NewGuid()
        );

        var response = await client.PostJsonAsync("/api/resources", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("   ", "Sub")]
    [InlineData("Title", "   ")]
    public async Task CreateBlankFieldReturnsValidationError(string title, string subtitle)
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateResourceRequest(
            title,
            subtitle,
            Description,
            null,
            SeedIds.ResourceTypes.Internal,
            thumbnailId
        );

        var response = await client.PostJsonAsync("/api/resources", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task CreateMissingCsrfTokenIsRejected()
    {
        var client = await LoginAsAdminAsync();
        var thumbnailId = await SeedThumbnailAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/resources")
        {
            Content = JsonContent.Create(
                new CreateResourceRequest(
                    "Gamma",
                    "Sub",
                    Description,
                    null,
                    SeedIds.ResourceTypes.Internal,
                    thumbnailId
                ),
                options: TestJson.Options
            ),
        };

        var response = await client.SendAsync(request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task UpdateReplacementThumbnailDeletesOrphanedOldFile()
    {
        var id = await SeedResourceAsync("Reemplazo");
        var oldThumbnailId = (await FindAsync<Resource>(id))!.ThumbnailId;
        var newThumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateResourceRequest(
            "Reemplazo",
            "Sub",
            Description,
            null,
            SeedIds.ResourceTypes.Internal,
            newThumbnailId
        );

        var response = await client.PutJsonAsync($"/api/resources/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldFile = await FindAsync<FileEntity>(oldThumbnailId);
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await FindAsync<FileEntity>(newThumbnailId);
        newFile.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateSwitchToExternalClearsDescriptionAndStoresUrl()
    {
        var id = await SeedResourceAsync("Cambiante");
        var thumbnailId = (await FindAsync<Resource>(id))!.ThumbnailId;
        var client = await LoginAsAdminAsync();
        var request = new UpdateResourceRequest(
            "Cambiante",
            "Sub",
            null,
            ExternalUrl,
            SeedIds.ResourceTypes.External,
            thumbnailId
        );

        var response = await client.PutJsonAsync($"/api/resources/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<Resource>(id);
        stored!.ResourceTypeId.Should().Be(SeedIds.ResourceTypes.External);
        stored.Url.Should().Be(ExternalUrl);
        stored.Description.Should().Be("{}");
    }

    [Theory]
    [InlineData("internal-with-url")]
    [InlineData("external-with-description")]
    [InlineData("external-without-url")]
    [InlineData("internal-without-description")]
    [InlineData("unknown-type")]
    public async Task UpdateTypeContentMismatchReturnsExpectedErrorCodeAndDoesNotPersist(
        string scenario
    )
    {
        var id = await SeedResourceAsync("Invariante");
        var thumbnailId = (await FindAsync<Resource>(id))!.ThumbnailId;
        var client = await LoginAsAdminAsync();
        var (request, expectedCode) = scenario switch
        {
            "internal-with-url" => (
                new UpdateResourceRequest(
                    "Title",
                    "Sub",
                    Description,
                    ExternalUrl,
                    SeedIds.ResourceTypes.Internal,
                    thumbnailId
                ),
                ErrorCode.ResourceUrlNotAllowed
            ),
            "external-with-description" => (
                new UpdateResourceRequest(
                    "Title",
                    "Sub",
                    Description,
                    ExternalUrl,
                    SeedIds.ResourceTypes.External,
                    thumbnailId
                ),
                ErrorCode.ResourceDescriptionNotAllowed
            ),
            "external-without-url" => (
                new UpdateResourceRequest(
                    "Title",
                    "Sub",
                    null,
                    null,
                    SeedIds.ResourceTypes.External,
                    thumbnailId
                ),
                ErrorCode.ResourceUrlRequired
            ),
            "internal-without-description" => (
                new UpdateResourceRequest(
                    "Title",
                    "Sub",
                    "{}",
                    null,
                    SeedIds.ResourceTypes.Internal,
                    thumbnailId
                ),
                ErrorCode.ResourceDescriptionRequired
            ),
            _ => (
                new UpdateResourceRequest(
                    "Title",
                    "Sub",
                    Description,
                    null,
                    Guid.NewGuid(),
                    thumbnailId
                ),
                ErrorCode.ResourceTypeNotFound
            ),
        };

        var response = await client.PutJsonAsync($"/api/resources/{id}", request, Ct);

        await response.ShouldBeBadRequestAsync(expectedCode);
        var stored = await FindAsync<Resource>(id);
        stored!.Title.Should().Be("Invariante");
    }

    [Fact]
    public async Task DeleteAdminRemovesResourceAndOrphanedThumbnail()
    {
        var id = await SeedResourceAsync("Doomed");
        var thumbnailId = (await FindAsync<Resource>(id))!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/resources/{id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<Resource>(id);
        stored.Should().BeNull();
        var file = await FindAsync<FileEntity>(thumbnailId);
        file.Should()
            .BeNull("the deleted resource's thumbnail is orphaned and must be cascade-deleted");
    }
}
