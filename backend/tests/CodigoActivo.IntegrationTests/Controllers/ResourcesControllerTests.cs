using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
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
    public async Task List_Anonymous_ReturnsPagedEnvelopeWithType()
    {
        await SeedResourceAsync("Alpha");
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/resources",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        var item = page.Items.Should().ContainSingle(r => r.Title == "Alpha").Subject;
        item.Type.Id.Should().Be(SeedIds.ResourceTypes.Internal);
        item.Type.Name.Should().Be("Interno");
        item.Type.IsExternal.Should().BeFalse();
        item.Url.Should().BeNull();
    }

    [Fact]
    public async Task List_FilterByResourceTypeId_ReturnsOnlyMatchingType()
    {
        await SeedResourceAsync("Guia interna");
        var externalId = await SeedResourceAsync("Curso externo", url: ExternalUrl);
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/resources?resourceTypeId={SeedIds.ResourceTypes.External}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        var item = page.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(externalId);
        item.Type.Id.Should().Be(SeedIds.ResourceTypes.External);
    }

    [Fact]
    public async Task List_FilterByUrl_MatchesAccentAndCaseInsensitively()
    {
        await SeedResourceAsync("Robotica", url: "https://ejemplo.es/robótica-avanzada");
        await SeedResourceAsync("Ajedrez", url: "https://ejemplo.es/ajedrez");
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/resources?url=ROBOTICA",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(r => r.Title == "Robotica");
    }

    [Fact]
    public async Task List_FilterByCreatedRange_UsesAppTimezoneDayBounds()
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

        var fromResponse = await client.GetAsync(
            "/api/resources?createdFrom=2026-03-10",
            TestContext.Current.CancellationToken
        );
        var toResponse = await client.GetAsync(
            "/api/resources?createdTo=2026-03-09",
            TestContext.Current.CancellationToken
        );

        fromResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fromPage = await fromResponse.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        fromPage!.Total.Should().Be(1);
        fromPage.Items.Should().ContainSingle(r => r.Title == "DiaDiez");

        toResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var toPage = await toResponse.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        toPage!.Total.Should().Be(1);
        toPage.Items.Should().ContainSingle(r => r.Title == "DiaNueve");
    }

    [Fact]
    public async Task List_SortByType_OrdersByTypeName()
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

        var response = await client.GetAsync(
            "/api/resources?sort=type",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Items.Select(r => r.Type.Name).Should().Equal("Externo", "Interno");
    }

    [Fact]
    public async Task List_SortByUrl_OrdersByUrlWithNullsLast()
    {
        await SeedResourceAsync("SinUrl");
        await SeedResourceAsync("UrlB", url: "https://beta.test/recurso");
        await SeedResourceAsync("UrlA", url: "https://alfa.test/recurso");
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/resources?sort=url",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<ResourceListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Items.Select(r => r.Title).Should().Equal("UrlA", "UrlB", "SinUrl");
    }

    [Fact]
    public async Task Types_Admin_ReturnsSeededTypesOrderedByName()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/resources/types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<ResourceTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        types!.Select(t => t.Name).Should().ContainInOrder("Externo", "Interno");
        types.Single(t => t.Name == "Externo").IsExternal.Should().BeTrue();
        types.Single(t => t.Name == "Interno").IsExternal.Should().BeFalse();
        types.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Color));
    }

    [Fact]
    public async Task Types_Member_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            "/api/resources/types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_ResourceAbsent_Returns404WithErrorCode()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/resources/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.ResourceNotFound);
    }

    [Fact]
    public async Task Get_ExternalResource_ReturnsTypeAndUrl()
    {
        var id = await SeedResourceAsync("Enlace", url: ExternalUrl);
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/resources/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resource = await response.ReadJsonAsync<ResourceResponse>(
            TestContext.Current.CancellationToken
        );
        resource!.Url.Should().Be(ExternalUrl);
        resource.Type.Id.Should().Be(SeedIds.ResourceTypes.External);
        resource.Type.IsExternal.Should().BeTrue();
    }

    [Fact]
    public async Task Create_Admin_PersistsAndReturns201WithLocation()
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

        var response = await client.PostJsonAsync(
            "/api/resources",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<ResourceResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Title.Should().Be("Gamma");
        created.Type.Id.Should().Be(SeedIds.ResourceTypes.Internal);

        var stored = await Factory.QueryAsync(db =>
            db.Resources.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Subtitle.Should().Be("Tagline");
        stored.ResourceTypeId.Should().Be(SeedIds.ResourceTypes.Internal);
        stored.Url.Should().BeNull();
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Create_ExternalResource_PersistsUrlWithoutDescription()
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

        var response = await client.PostJsonAsync(
            "/api/resources",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<ResourceResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Url.Should().Be(ExternalUrl);
        created.Type.IsExternal.Should().BeTrue();

        var stored = await Factory.QueryAsync(db =>
            db.Resources.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
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
    public async Task Create_TypeContentMismatch_ReturnsExpectedErrorCode(string scenario)
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

        var response = await client.PostJsonAsync(
            "/api/resources",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public async Task Create_MalformedUrl_ReturnsValidationError()
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

        var response = await client.PostJsonAsync(
            "/api/resources",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Create_Member_ReturnsForbidden()
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

        var response = await client.PostJsonAsync(
            "/api/resources",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
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

        var response = await client.PostJsonAsync(
            "/api/resources",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("   ", "Sub")]
    [InlineData("Title", "   ")]
    public async Task Create_BlankField_ReturnsValidationError(string title, string subtitle)
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

        var response = await client.PostJsonAsync(
            "/api/resources",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Create_MissingCsrfToken_IsRejected()
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

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Update_ReplacementThumbnail_DeletesOrphanedOldFile()
    {
        var id = await SeedResourceAsync("Reemplazo");
        var oldThumbnailId = (
            await Factory.QueryAsync(db =>
                db.Resources.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
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

        var response = await client.PutJsonAsync(
            $"/api/resources/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldFile = await Factory.QueryAsync(db =>
            db.Files.FindAsync([oldThumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await Factory.QueryAsync(db =>
            db.Files.FindAsync([newThumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        newFile.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_SwitchToExternal_ClearsDescriptionAndStoresUrl()
    {
        var id = await SeedResourceAsync("Cambiante");
        var thumbnailId = (
            await Factory.QueryAsync(db =>
                db.Resources.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
        var client = await LoginAsAdminAsync();
        var request = new UpdateResourceRequest(
            "Cambiante",
            "Sub",
            null,
            ExternalUrl,
            SeedIds.ResourceTypes.External,
            thumbnailId
        );

        var response = await client.PutJsonAsync(
            $"/api/resources/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Resources.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
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
    public async Task Update_TypeContentMismatch_ReturnsExpectedErrorCodeAndDoesNotPersist(
        string scenario
    )
    {
        var id = await SeedResourceAsync("Invariante");
        var thumbnailId = (
            await Factory.QueryAsync(db =>
                db.Resources.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
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

        var response = await client.PutJsonAsync(
            $"/api/resources/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(expectedCode);
        var stored = await Factory.QueryAsync(db =>
            db.Resources.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Title.Should().Be("Invariante");
    }

    [Fact]
    public async Task Delete_Admin_RemovesResourceAndOrphanedThumbnail()
    {
        var id = await SeedResourceAsync("Doomed");
        var thumbnailId = (
            await Factory.QueryAsync(db =>
                db.Resources.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/resources/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Resources.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored.Should().BeNull();
        var file = await Factory.QueryAsync(db =>
            db.Files.FindAsync([thumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        file.Should()
            .BeNull("the deleted resource's thumbnail is orphaned and must be cascade-deleted");
    }
}
