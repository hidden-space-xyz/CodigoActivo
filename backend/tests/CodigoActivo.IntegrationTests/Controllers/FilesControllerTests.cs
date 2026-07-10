using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class FilesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static byte[] ValidPng()
    {
        return
        [
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A,
            0x00,
            0x00,
            0x00,
            0x0D,
            0x49,
            0x48,
            0x44,
            0x52,
            0x00,
            0x00,
            0x00,
            0x01,
            0x00,
            0x00,
            0x00,
            0x01,
            0x08,
            0x06,
            0x00,
            0x00,
            0x00,
            0x1F,
            0x15,
            0xC4,
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
        if (withCsrf)
            request.Headers.Add(
                "X-CSRF-TOKEN",
                await client.FetchCsrfTokenAsync(TestContext.Current.CancellationToken)
            );

        var form = new MultipartFormDataContent();
        if (fileBytes is not null)
        {
            var part = new ByteArrayContent(fileBytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(partContentType);
            form.Add(part, "file", fileName);
        }

        request.Content = form;
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<FileResponse> UploadAsAdminAsync(
        byte[]? bytes = null,
        string fileName = "image.png"
    )
    {
        var client = await LoginAsAdminAsync();
        using var response = await SendUploadAsync(
            client,
            HttpMethod.Post,
            "/api/files",
            bytes ?? ValidPng(),
            fileName
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync<FileResponse>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndPersistsFile()
    {
        var bytes = ValidPng();
        var client = await LoginAsAdminAsync();

        using var response = await SendUploadAsync(
            client,
            HttpMethod.Post,
            "/api/files",
            bytes,
            "picture.png"
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<FileResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Name.Should().Be("picture.png");
        created.Extension.Should().Be("png");
        created.UploadedBy.Should().Be(TestSeedData.Users.AdminId);
        created.UploadedAt.Should().Be(Factory.Clock.UtcNow);
        response.Headers.Location!.ToString().Should().EndWith($"/api/files/{created.Id}");

        var stored = await Factory.QueryAsync(db =>
            db.Files.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Extension.Should().Be("png");
        stored.UploadedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Create_AsMember_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        using var response = await SendUploadAsync(
            client,
            HttpMethod.Post,
            "/api/files",
            ValidPng()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        using var response = await SendUploadAsync(
            client,
            HttpMethod.Post,
            "/api/files",
            ValidPng()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_MissingCsrfToken_ReturnsBadRequestInvalidCsrf()
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
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Create_MissingFilePart_ReturnsBadRequestValidationFailed()
    {
        var client = await LoginAsAdminAsync();
        var before = await Factory.QueryAsync(db =>
            db.Files.CountAsync(TestContext.Current.CancellationToken)
        );

        using var response = await SendUploadAsync(
            client,
            HttpMethod.Post,
            "/api/files",
            fileBytes: null
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);

        var after = await Factory.QueryAsync(db =>
            db.Files.CountAsync(TestContext.Current.CancellationToken)
        );
        after.Should().Be(before);
    }

    [Fact]
    public async Task Create_EmptyFile_ReturnsBadRequestFileUploadEmpty()
    {
        var client = await LoginAsAdminAsync();

        using var response = await SendUploadAsync(client, HttpMethod.Post, "/api/files", []);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.FileUploadEmpty);
    }

    [Fact]
    public async Task Get_Anonymous_ReturnsUploadedFileMetadata()
    {
        var created = await UploadAsAdminAsync(fileName: "avatar.png");
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/files/{created.Id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var meta = await response.ReadJsonAsync<FileResponse>(
            TestContext.Current.CancellationToken
        );
        meta!.Id.Should().Be(created.Id);
        meta.Name.Should().Be("avatar.png");
        meta.Extension.Should().Be("png");
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFoundFileNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/files/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.FileNotFound);
    }

    [Fact]
    public async Task GetContent_ExistingFile_ReturnsStoredBytesAndContentType()
    {
        var bytes = ValidPng();
        var created = await UploadAsAdminAsync(bytes, "photo.png");
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/files/{created.Id}/content",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var downloaded = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        downloaded.Should().Equal(bytes);
    }

    [Fact]
    public async Task GetContent_BlobMissingFromStorage_ReturnsNotFoundStorageMissing()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = id,
                    Name = "orphan",
                    Extension = "png",
                    UploadedAt = Factory.Clock.UtcNow,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/files/{id}/content",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.FileContentMissingFromStorage);
    }

    [Fact]
    public async Task GetContent_IfNoneMatchMatchesEtag_ReturnsNotModified()
    {
        var created = await UploadAsAdminAsync(ValidPng(), "cached.png");
        var client = CreateClient();

        using var first = await client.GetAsync(
            $"/api/files/{created.Id}/content",
            TestContext.Current.CancellationToken
        );
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var etag = first.Headers.ETag;
        etag.Should().NotBeNull();
        etag!.IsWeak.Should().BeFalse();
        etag.Tag.Should().Be($"\"{created.Id:N}-{created.UploadedAt.UtcTicks}\"");

        using var conditional = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/files/{created.Id}/content"
        );
        conditional.Headers.IfNoneMatch.Add(etag);

        using var response = await client.SendAsync(
            conditional,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
        var body = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_AsAdmin_ReplacesContentAndNameKeepingId()
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
        var updated = await response.ReadJsonAsync<FileResponse>(
            TestContext.Current.CancellationToken
        );
        updated!.Id.Should().Be(created.Id);
        updated.Name.Should().Be("new.png");

        var stored = await Factory.QueryAsync(db =>
            db.Files.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Name.Should().Be("new.png");
        stored.Extension.Should().Be("png");
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesFileAndSubsequentGetIsNotFound()
    {
        var created = await UploadAsAdminAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/files/{created.Id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Files.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored.Should().BeNull();

        using var followUp = await CreateClient()
            .GetAsync($"/api/files/{created.Id}", TestContext.Current.CancellationToken);
        followUp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_FileEmbeddedInDescription_ReturnsConflictFileInUse()
    {
        var created = await UploadAsAdminAsync();
        var thumbnailId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = thumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = Factory.Clock.UtcNow,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Announcements.Add(
                new Announcement
                {
                    Id = Guid.NewGuid(),
                    Title = "Con imagen",
                    Subtitle = "Sub",
                    Description = $"{{\"img\":\"/api/files/{created.Id}/content\"}}",
                    ThumbnailId = thumbnailId,
                    CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/files/{created.Id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.FileInUse);
        var stored = await Factory.QueryAsync(db =>
            db.Files.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored
            .Should()
            .NotBeNull("a file embedded in a rich-text description must survive deletion");
    }
}
