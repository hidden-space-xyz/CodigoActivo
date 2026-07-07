using System.Net;
using System.Net.Http.Headers;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class FilesControllerTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{

    private static byte[] ValidPng()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        ];
    }

    private static async Task<HttpResponseMessage> SendUploadAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        byte[]? fileBytes,
        string fileName = "image.png",
        string partContentType = "image/png",
        bool withCsrf = true
    )
    {
        using var request = new HttpRequestMessage(method, url);
        if (withCsrf) request.Headers.Add("X-CSRF-TOKEN", await client.FetchCsrfTokenAsync());

        var form = new MultipartFormDataContent();
        if (fileBytes is not null)
        {
            var part = new ByteArrayContent(fileBytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(partContentType);
            form.Add(part, "file", fileName);
        }

        request.Content = form;
        return await client.SendAsync(request);
    }

    private async Task<FileResponse> UploadAsAdminAsync(byte[]? bytes = null, string fileName = "image.png")
    {
        var client = await LoginAsAdminAsync();
        using var response = await SendUploadAsync(client, HttpMethod.Post, "/api/files", bytes ?? ValidPng(), fileName);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync<FileResponse>())!;
    }

    [Fact]
    public async Task Create_as_admin_persists_file_and_returns_201_with_location()
    {
        var bytes = ValidPng();
        var client = await LoginAsAdminAsync();

        using var response = await SendUploadAsync(client, HttpMethod.Post, "/api/files", bytes, "picture.png");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<FileResponse>();
        created!.Name.Should().Be("picture.png");
        created.Extension.Should().Be("png");
        created.UploadedBy.Should().Be(TestSeedData.Users.AdminId);
        created.UploadedAt.Should().Be(Factory.Clock.UtcNow);
        response.Headers.Location!.ToString().Should().EndWith($"/api/files/{created.Id}");

        var stored = await Factory.QueryAsync(db => db.Files.FindAsync(created.Id).AsTask());
        stored!.Extension.Should().Be("png");
        stored.UploadedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Create_as_member_is_forbidden()
    {
        var client = await LoginAsMemberAsync();

        using var response = await SendUploadAsync(client, HttpMethod.Post, "/api/files", ValidPng());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        using var response = await SendUploadAsync(client, HttpMethod.Post, "/api/files", ValidPng());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_without_csrf_token_is_rejected()
    {
        var client = await LoginAsAdminAsync();

        using var response = await SendUploadAsync(
            client,
            HttpMethod.Post,
            "/api/files",
            ValidPng(),
            withCsrf: false
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Create_without_file_part_is_bad_request_with_upload_missing()
    {
        var client = await LoginAsAdminAsync();
        var before = await Factory.QueryAsync(db => db.Files.CountAsync());

        using var response = await SendUploadAsync(client, HttpMethod.Post, "/api/files", fileBytes: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);

        var after = await Factory.QueryAsync(db => db.Files.CountAsync());
        after.Should().Be(before);
    }

    [Fact]
    public async Task Create_with_empty_file_is_bad_request_with_upload_empty()
    {
        var client = await LoginAsAdminAsync();

        using var response = await SendUploadAsync(client, HttpMethod.Post, "/api/files", []);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.FileUploadEmpty);
    }

    [Fact]
    public async Task Get_metadata_is_anonymous_and_returns_uploaded_file()
    {
        var created = await UploadAsAdminAsync(fileName: "avatar.png");
        var client = CreateClient();

        var response = await client.GetAsync($"/api/files/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var meta = await response.ReadJsonAsync<FileResponse>();
        meta!.Id.Should().Be(created.Id);
        meta.Name.Should().Be("avatar.png");
        meta.Extension.Should().Be("png");
    }

    [Fact]
    public async Task Get_metadata_for_unknown_id_is_404_with_file_not_found()
    {
        var client = CreateClient();

        var response = await client.GetAsync($"/api/files/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.FileNotFound);
    }

    [Fact]
    public async Task Get_content_returns_the_stored_bytes_and_detected_content_type()
    {
        var bytes = ValidPng();
        var created = await UploadAsAdminAsync(bytes, "photo.png");
        var client = CreateClient();

        var response = await client.GetAsync($"/api/files/{created.Id}/content");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var downloaded = await response.Content.ReadAsByteArrayAsync();
        downloaded.Should().Equal(bytes);
    }

    [Fact]
    public async Task Get_content_when_metadata_exists_but_blob_is_missing_is_404_with_storage_missing()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(new FileEntity
            {
                Id = id,
                Name = "orphan",
                Extension = "png",
                UploadedAt = Factory.Clock.UtcNow,
                UploadedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
        var client = CreateClient();

        var response = await client.GetAsync($"/api/files/{id}/content");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.FileContentMissingFromStorage);
    }

    [Fact]
    public async Task Update_as_admin_replaces_content_and_name_keeping_same_id()
    {
        var created = await UploadAsAdminAsync(fileName: "old.png");
        var client = await LoginAsAdminAsync();

        using var response = await SendUploadAsync(
            client,
            HttpMethod.Put,
            $"/api/files/{created.Id}",
            ValidPng(),
            "new.png"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<FileResponse>();
        updated!.Id.Should().Be(created.Id);
        updated.Name.Should().Be("new.png");

        var stored = await Factory.QueryAsync(db => db.Files.FindAsync(created.Id).AsTask());
        stored!.Name.Should().Be("new.png");
        stored.Extension.Should().Be("png");
    }

    [Fact]
    public async Task Delete_as_admin_removes_the_file_then_get_is_404()
    {
        var created = await UploadAsAdminAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/files/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Files.FindAsync(created.Id).AsTask());
        stored.Should().BeNull();

        using var followUp = await CreateClient().GetAsync($"/api/files/{created.Id}");
        followUp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_file_embedded_in_a_description_is_conflict_file_in_use()
    {
        var created = await UploadAsAdminAsync();
        var thumbnailId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(new FileEntity
            {
                Id = thumbnailId,
                Name = "thumb",
                Extension = "png",
                UploadedAt = Factory.Clock.UtcNow,
                UploadedBy = TestSeedData.Users.AdminId,
            });
            db.Announcements.Add(new Announcement
            {
                Id = Guid.NewGuid(),
                Title = "Con imagen",
                Subtitle = "Sub",
                Description = $"{{\"img\":\"/api/files/{created.Id}/content\"}}",
                ThumbnailId = thumbnailId,
                CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = TestSeedData.Users.AdminId,
            });
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/files/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.FileInUse);
        var stored = await Factory.QueryAsync(db => db.Files.FindAsync(created.Id).AsTask());
        stored.Should().NotBeNull("a file embedded in a rich-text description must survive deletion");
    }

}
