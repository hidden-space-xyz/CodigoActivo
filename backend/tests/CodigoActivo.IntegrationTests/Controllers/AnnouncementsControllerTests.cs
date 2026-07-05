using System.Net;
using System.Net.Http.Json;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

/// <summary>
/// Integration coverage for the announcements CRUD controller: anonymous reads (list paging + filters,
/// get, years), admin-only writes with the full auth matrix, CSRF enforcement, model validation, the
/// not-found + missing-thumbnail contracts, the set-featured endpoint, and persistence read from the store.
/// </summary>
public sealed class AnnouncementsControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Description = "{}";

    private async Task<Guid> SeedThumbnailAsync()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(new FileEntity
            {
                Id = id,
                Name = "thumb",
                Extension = "png",
                UploadedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UploadedBy = TestSeedData.Users.AdminId,
            });
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
            db.Announcements.Add(new Announcement
            {
                Id = id,
                Title = title,
                Subtitle = subtitle,
                Description = Description,
                Featured = featured,
                ThumbnailId = thumbnailId,
                CreatedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
        return id;
    }

    // ---- Reads (anonymous) -------------------------------------------------

    [Fact]
    public async Task List_is_anonymous_and_returns_paged_envelope()
    {
        await SeedAnnouncementAsync("Alpha");
        var client = CreateClient();

        var response = await client.GetAsync("/api/announcements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<AnnouncementListItemResponse>>();
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.Items.Should().ContainSingle(a => a.Title == "Alpha");
    }

    [Fact]
    public async Task List_filters_by_title_and_featured()
    {
        await SeedAnnouncementAsync("Keep Me", featured: true);
        await SeedAnnouncementAsync("Other", featured: false);
        var client = CreateClient();

        var response = await client.GetAsync("/api/announcements?title=keep&featured=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<AnnouncementListItemResponse>>();
        page!.Items.Should().ContainSingle(a => a.Title == "Keep Me");
        page.Total.Should().Be(1);
    }

    [Fact]
    public async Task List_filters_by_year()
    {
        await SeedAnnouncementAsync("Old", year: 2020);
        await SeedAnnouncementAsync("New", year: 2025);
        var client = CreateClient();

        var response = await client.GetAsync("/api/announcements?year=2025");

        var page = await response.ReadJsonAsync<PagedResult<AnnouncementListItemResponse>>();
        page!.Items.Should().ContainSingle(a => a.Title == "New");
    }

    [Fact]
    public async Task Years_is_anonymous_and_returns_distinct_descending_years()
    {
        await SeedAnnouncementAsync("A", year: 2021);
        await SeedAnnouncementAsync("B", year: 2023);
        await SeedAnnouncementAsync("C", year: 2021);
        var client = CreateClient();

        var response = await client.GetAsync("/api/announcements/years");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var years = await response.ReadJsonAsync<IReadOnlyList<int>>();
        years.Should().Equal(2023, 2021);
    }

    [Fact]
    public async Task Get_returns_announcement_when_present()
    {
        var id = await SeedAnnouncementAsync("Beta");
        var client = CreateClient();

        var response = await client.GetAsync($"/api/announcements/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var announcement = await response.ReadJsonAsync<AnnouncementResponse>();
        announcement!.Title.Should().Be("Beta");
    }

    [Fact]
    public async Task Get_returns_404_with_error_code_when_absent()
    {
        var client = CreateClient();

        var response = await client.GetAsync($"/api/announcements/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    // ---- Create (admin only) ----------------------------------------------

    [Fact]
    public async Task Create_as_admin_persists_and_returns_201_with_location()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateAnnouncementRequest("Gamma", "Tagline", Description, thumbnailId);

        var response = await client.PostJsonAsync("/api/announcements", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<AnnouncementResponse>();
        created!.Title.Should().Be("Gamma");

        var stored = await Factory.QueryAsync(db => db.Announcements.FindAsync(created.Id).AsTask());
        stored!.Subtitle.Should().Be("Tagline");
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        stored.Featured.Should().BeFalse();
    }

    [Fact]
    public async Task Create_as_member_is_forbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsMemberAsync();
        var request = new CreateAnnouncementRequest("Nope", "Sub", Description, thumbnailId);

        var response = await client.PostJsonAsync("/api/announcements", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_anonymous_is_unauthorized()
    {
        var client = CreateClient();
        var request = new CreateAnnouncementRequest("Nope", "Sub", Description, Guid.NewGuid());

        var response = await client.PostJsonAsync("/api/announcements", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("   ", "Sub")]
    [InlineData("Title", "   ")]
    public async Task Create_with_blank_field_is_validation_error(string title, string subtitle)
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateAnnouncementRequest(title, subtitle, Description, thumbnailId);

        var response = await client.PostJsonAsync("/api/announcements", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Create_with_missing_thumbnail_is_bad_request()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateAnnouncementRequest("Gamma", "Sub", Description, Guid.NewGuid());

        var response = await client.PostJsonAsync("/api/announcements", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
    }

    [Fact]
    public async Task Post_without_csrf_token_is_rejected()
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

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    // ---- Update ------------------------------------------------------------

    [Fact]
    public async Task Update_as_admin_changes_announcement()
    {
        var id = await SeedAnnouncementAsync("Before");
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateAnnouncementRequest("After", "NewSub", Description, thumbnailId);

        var response = await client.PutJsonAsync($"/api/announcements/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db => db.Announcements.FindAsync(id).AsTask());
        stored!.Title.Should().Be("After");
        stored.Subtitle.Should().Be("NewSub");
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Update_missing_announcement_is_404()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateAnnouncementRequest("X", "Y", Description, thumbnailId);

        var response = await client.PutJsonAsync($"/api/announcements/{Guid.NewGuid()}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task Update_with_replacement_thumbnail_deletes_the_orphaned_old_file()
    {
        var id = await SeedAnnouncementAsync("Reemplazo");
        var oldThumbnailId = (await Factory.QueryAsync(db => db.Announcements.FindAsync(id).AsTask()))!.ThumbnailId;
        var newThumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateAnnouncementRequest("Reemplazo", "Sub", Description, newThumbnailId);

        var response = await client.PutJsonAsync($"/api/announcements/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldFile = await Factory.QueryAsync(db => db.Files.FindAsync(oldThumbnailId).AsTask());
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await Factory.QueryAsync(db => db.Files.FindAsync(newThumbnailId).AsTask());
        newFile.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_removing_an_embedded_description_image_deletes_the_orphaned_file()
    {
        var id = await SeedAnnouncementAsync("Con imagen");
        var thumbnailId = (await Factory.QueryAsync(db => db.Announcements.FindAsync(id).AsTask()))!.ThumbnailId;
        var embeddedFileId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var withImage = new UpdateAnnouncementRequest(
            "Con imagen",
            "Sub",
            $"{{\"img\":\"/api/files/{embeddedFileId}/content\"}}",
            thumbnailId
        );
        using (var seeded = await client.PutJsonAsync($"/api/announcements/{id}", withImage))
        {
            seeded.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var withoutImage = new UpdateAnnouncementRequest("Con imagen", "Sub", Description, thumbnailId);

        var response = await client.PutJsonAsync($"/api/announcements/{id}", withoutImage);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var file = await Factory.QueryAsync(db => db.Files.FindAsync(embeddedFileId).AsTask());
        file.Should().BeNull("an image dropped from the description is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task Update_with_missing_thumbnail_is_bad_request()
    {
        var id = await SeedAnnouncementAsync("Before");
        var client = await LoginAsAdminAsync();
        var request = new UpdateAnnouncementRequest("After", "Sub", Description, Guid.NewGuid());

        var response = await client.PutJsonAsync($"/api/announcements/{id}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
    }

    // ---- Feature (set-featured) -------------------------------------------

    [Fact(Skip = "SetFeatured -> repo.SetFeaturedAsync uses EF ExecuteUpdateAsync, unsupported by the in-memory provider (needs a relational DB / Docker). Service logic covered by AnnouncementService unit tests.")]
    public async Task Feature_as_admin_sets_featured_true()
    {
        var id = await SeedAnnouncementAsync("Feature Me", featured: false);
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync($"/api/announcements/{id}/feature");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<AnnouncementResponse>();
        body!.Featured.Should().BeTrue();
        var stored = await Factory.QueryAsync(db => db.Announcements.FindAsync(id).AsTask());
        stored!.Featured.Should().BeTrue();
    }

    [Fact(Skip = "SetFeatured -> repo.SetFeaturedAsync uses EF ExecuteUpdateAsync, unsupported by the in-memory provider (needs a relational DB / Docker). Service logic covered by AnnouncementService unit tests.")]
    public async Task Feature_missing_announcement_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync($"/api/announcements/{Guid.NewGuid()}/feature");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task Feature_as_member_is_forbidden()
    {
        var id = await SeedAnnouncementAsync("Guarded");
        var client = await LoginAsMemberAsync();

        var response = await client.PatchJsonAsync($"/api/announcements/{id}/feature");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Delete ------------------------------------------------------------

    [Fact]
    public async Task Delete_as_admin_removes_announcement_and_its_orphaned_thumbnail()
    {
        var id = await SeedAnnouncementAsync("Doomed");
        var thumbnailId = (await Factory.QueryAsync(db => db.Announcements.FindAsync(id).AsTask()))!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/announcements/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Announcements.FindAsync(id).AsTask());
        stored.Should().BeNull();
        var file = await Factory.QueryAsync(db => db.Files.FindAsync(thumbnailId).AsTask());
        file.Should().BeNull("the deleted announcement's thumbnail is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task Delete_keeps_a_thumbnail_still_shared_with_another_announcement()
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

        var response = await client.DeleteWithCsrfAsync($"/api/announcements/{doomedId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var file = await Factory.QueryAsync(db => db.Files.FindAsync(sharedThumbnailId).AsTask());
        file.Should().NotBeNull("a thumbnail still referenced by another entity must survive the cascade");
        var survivor = await Factory.QueryAsync(db => db.Announcements.FindAsync(survivorId).AsTask());
        survivor.Should().NotBeNull();
    }

    private static Announcement NewSharedThumbnailAnnouncement(Guid id, string title, Guid thumbnailId) =>
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

    [Fact]
    public async Task Delete_missing_announcement_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/announcements/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }
}
