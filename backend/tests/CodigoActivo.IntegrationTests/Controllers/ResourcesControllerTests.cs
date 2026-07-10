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

public sealed class ResourcesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Description = "{}";

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

    private async Task<Guid> SeedResourceAsync(string title = "Existing", string subtitle = "Sub")
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
                    Description = Description,
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
    public async Task List_Anonymous_ReturnsPagedEnvelope()
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
        page.Items.Should().ContainSingle(r => r.Title == "Alpha");
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
    public async Task Create_Admin_PersistsAndReturns201WithLocation()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateResourceRequest("Gamma", "Tagline", Description, thumbnailId);

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

        var stored = await Factory.QueryAsync(db =>
            db.Resources.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Subtitle.Should().Be("Tagline");
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Create_Member_ReturnsForbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsMemberAsync();
        var request = new CreateResourceRequest("Nope", "Sub", Description, thumbnailId);

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
        var request = new CreateResourceRequest("Nope", "Sub", Description, Guid.NewGuid());

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
        var request = new CreateResourceRequest(title, subtitle, Description, thumbnailId);

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
                new CreateResourceRequest("Gamma", "Sub", Description, thumbnailId),
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
        var request = new UpdateResourceRequest("Reemplazo", "Sub", Description, newThumbnailId);

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
