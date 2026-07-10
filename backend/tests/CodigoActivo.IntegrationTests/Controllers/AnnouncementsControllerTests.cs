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

public sealed class AnnouncementsControllerTests(CodigoActivoWebAppFactory factory)
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

    private async Task<Guid> SeedAnnouncementAsync(
        string title = "Existing",
        string subtitle = "Sub",
        bool featured = false,
        int year = 2024
    )
    {
        var thumbnailId = await SeedThumbnailAsync();
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Announcements.Add(
                new Announcement
                {
                    Id = id,
                    Title = title,
                    Subtitle = subtitle,
                    Description = Description,
                    Featured = featured,
                    ThumbnailId = thumbnailId,
                    CreatedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

    [Fact]
    public async Task List_NoFilters_ReturnsOkPagedEnvelope()
    {
        await SeedAnnouncementAsync("Alpha");
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/announcements",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<AnnouncementListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.Items.Should().ContainSingle(a => a.Title == "Alpha");
    }

    [Fact]
    public async Task Years_DuplicateYears_ReturnsDistinctDescending()
    {
        await SeedAnnouncementAsync("A", year: 2021);
        await SeedAnnouncementAsync("B", year: 2023);
        await SeedAnnouncementAsync("C", year: 2021);
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/announcements/years",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var years = await response.ReadJsonAsync<IReadOnlyList<int>>(
            TestContext.Current.CancellationToken
        );
        years.Should().Equal(2023, 2021);
    }

    [Fact]
    public async Task Get_AnnouncementExists_ReturnsOkWithAnnouncement()
    {
        var id = await SeedAnnouncementAsync("Beta");
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/announcements/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var announcement = await response.ReadJsonAsync<AnnouncementResponse>(
            TestContext.Current.CancellationToken
        );
        announcement!.Title.Should().Be("Beta");
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndPersists()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateAnnouncementRequest("Gamma", "Tagline", Description, thumbnailId);

        var response = await client.PostJsonAsync(
            "/api/announcements",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<AnnouncementResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Title.Should().Be("Gamma");

        var stored = await Factory.QueryAsync(db =>
            db.Announcements.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Subtitle.Should().Be("Tagline");
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        stored.Featured.Should().BeFalse();
    }

    [Fact]
    public async Task Create_AsMember_ReturnsForbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsMemberAsync();
        var request = new CreateAnnouncementRequest("Nope", "Sub", Description, thumbnailId);

        var response = await client.PostJsonAsync(
            "/api/announcements",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();
        var request = new CreateAnnouncementRequest("Nope", "Sub", Description, Guid.NewGuid());

        var response = await client.PostJsonAsync(
            "/api/announcements",
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
        var request = new CreateAnnouncementRequest(title, subtitle, Description, thumbnailId);

        var response = await client.PostJsonAsync(
            "/api/announcements",
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
    public async Task Create_MissingCsrfToken_ReturnsBadRequest()
    {
        var client = await LoginAsAdminAsync();
        var thumbnailId = await SeedThumbnailAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/announcements")
        {
            Content = JsonContent.Create(
                new CreateAnnouncementRequest("Gamma", "Sub", Description, thumbnailId),
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
    public async Task Update_AsAdmin_PersistsChanges()
    {
        var id = await SeedAnnouncementAsync("Before");
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateAnnouncementRequest("After", "NewSub", Description, thumbnailId);

        var response = await client.PutJsonAsync(
            $"/api/announcements/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Announcements.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Title.Should().Be("After");
        stored.Subtitle.Should().Be("NewSub");
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Update_ReplacesThumbnail_DeletesOrphanedOldFile()
    {
        var id = await SeedAnnouncementAsync("Reemplazo");
        var oldThumbnailId = (
            await Factory.QueryAsync(db =>
                db.Announcements.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
        var newThumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateAnnouncementRequest(
            "Reemplazo",
            "Sub",
            Description,
            newThumbnailId
        );

        var response = await client.PutJsonAsync(
            $"/api/announcements/{id}",
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
    public async Task Update_RemovesEmbeddedImage_DeletesOrphanedFile()
    {
        var id = await SeedAnnouncementAsync("Con imagen");
        var thumbnailId = (
            await Factory.QueryAsync(db =>
                db.Announcements.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
        var embeddedFileId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var withImage = new UpdateAnnouncementRequest(
            "Con imagen",
            "Sub",
            $"{{\"img\":\"/api/files/{embeddedFileId}/content\"}}",
            thumbnailId
        );
        using (
            var seeded = await client.PutJsonAsync(
                $"/api/announcements/{id}",
                withImage,
                TestContext.Current.CancellationToken
            )
        )
        {
            seeded.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var withoutImage = new UpdateAnnouncementRequest(
            "Con imagen",
            "Sub",
            Description,
            thumbnailId
        );

        var response = await client.PutJsonAsync(
            $"/api/announcements/{id}",
            withoutImage,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var file = await Factory.QueryAsync(db =>
            db.Files.FindAsync([embeddedFileId], TestContext.Current.CancellationToken).AsTask()
        );
        file.Should()
            .BeNull(
                "an image dropped from the description is orphaned and must be cascade-deleted"
            );
    }

    [Fact]
    public async Task Feature_AnnouncementMissing_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/announcements/{Guid.NewGuid()}/feature",
            ct: TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesAnnouncementAndOrphanedThumbnail()
    {
        var id = await SeedAnnouncementAsync("Doomed");
        var thumbnailId = (
            await Factory.QueryAsync(db =>
                db.Announcements.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/announcements/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Announcements.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored.Should().BeNull();
        var file = await Factory.QueryAsync(db =>
            db.Files.FindAsync([thumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        file.Should()
            .BeNull("the deleted announcement's thumbnail is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task Delete_SharedThumbnail_KeepsThumbnailAndSurvivor()
    {
        var sharedThumbnailId = await SeedThumbnailAsync();
        var doomedId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Announcements.AddRange(
                NewSharedThumbnailAnnouncement(doomedId, "Doomed", sharedThumbnailId),
                NewSharedThumbnailAnnouncement(survivorId, "Survivor", sharedThumbnailId)
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/announcements/{doomedId}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var file = await Factory.QueryAsync(db =>
            db.Files.FindAsync([sharedThumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        file.Should()
            .NotBeNull("a thumbnail still referenced by another entity must survive the cascade");
        var survivor = await Factory.QueryAsync(db =>
            db.Announcements.FindAsync([survivorId], TestContext.Current.CancellationToken).AsTask()
        );
        survivor.Should().NotBeNull();
    }

    private static Announcement NewSharedThumbnailAnnouncement(
        Guid id,
        string title,
        Guid thumbnailId
    ) =>
        new()
        {
            Id = id,
            Title = title,
            Subtitle = "Sub",
            Description = Description,
            ThumbnailId = thumbnailId,
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = TestSeedData.Users.AdminId,
        };
}
