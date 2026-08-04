using System.Globalization;
using System.Net;
using AwesomeAssertions;
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
    private async Task<FileResponse> UploadAsAdminAsync(
        byte[]? bytes = null,
        string fileName = "image.png"
    )
    {
        var client = await LoginAsAdminAsync();
        using var response = await client.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            bytes ?? TestSeedData.ValidPng(),
            fileName
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync<FileResponse>(Ct))!;
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndPersistsFile()
    {
        var bytes = TestSeedData.ValidPng();
        var client = await LoginAsAdminAsync();

        using var response = await client.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            bytes,
            "picture.png"
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<FileResponse>(Ct);
        created!.Name.Should().Be("picture.png");
        created.Extension.Should().Be("png");
        created.UploadedBy.Should().Be(TestSeedData.Users.AdminId);
        created.UploadedAt.Should().Be(Factory.Clock.UtcNow);
        response.Headers.Location!.ToString().Should().EndWith($"/api/files/{created.Id}");

        var stored = await FindAsync<FileEntity>(created.Id);
        stored!.Extension.Should().Be("png");
        stored.UploadedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Create_AsMember_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        using var response = await client.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            TestSeedData.ValidPng()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        using var response = await client.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            TestSeedData.ValidPng()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_MissingCsrfToken_ReturnsBadRequestInvalidCsrf()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            TestSeedData.ValidPng(),
            withCsrf: false
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task Create_MissingFilePart_ReturnsBadRequestValidationFailed()
    {
        var client = await LoginAsAdminAsync();
        var before = await Factory.QueryAsync(db => db.Files.CountAsync(Ct));

        using var response = await client.SendUploadAsync(
            HttpMethod.Post,
            "/api/files",
            fileBytes: null
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);

        var after = await Factory.QueryAsync(db => db.Files.CountAsync(Ct));
        after.Should().Be(before);
    }

    [Fact]
    public async Task Create_EmptyFile_ReturnsBadRequestFileUploadEmpty()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendUploadAsync(HttpMethod.Post, "/api/files", []);

        await response.ShouldBeBadRequestAsync(ErrorCode.FileUploadEmpty);
    }

    [Fact]
    public async Task Get_Anonymous_ReturnsUploadedFileMetadata()
    {
        var created = await UploadAsAdminAsync(fileName: "avatar.png");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/files/{created.Id}"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var meta = await response.ReadJsonAsync<FileResponse>(Ct);
        meta!.Id.Should().Be(created.Id);
        meta.Name.Should().Be("avatar.png");
        meta.Extension.Should().Be("png");
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFoundFileNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/files/{Guid.NewGuid()}"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.FileNotFound);
    }

    [Fact]
    public async Task GetContent_ExistingFile_ReturnsStoredBytesAndContentType()
    {
        var bytes = TestSeedData.ValidPng();
        var created = await UploadAsAdminAsync(bytes, "photo.png");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/files/{created.Id}/content"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var downloaded = await response.Content.ReadAsByteArrayAsync(Ct);
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

        var response = await client.GetAsync(TestUri.Rel($"/api/files/{id}/content"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.FileContentMissingFromStorage);
    }

    [Fact]
    public async Task GetContent_IfNoneMatchMatchesEtag_ReturnsNotModified()
    {
        var created = await UploadAsAdminAsync(TestSeedData.ValidPng(), "cached.png");
        var client = CreateClient();

        using var first = await client.GetAsync(TestUri.Rel($"/api/files/{created.Id}/content"), Ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var etag = first.Headers.ETag;
        etag.Should().NotBeNull();
        etag.IsWeak.Should().BeFalse();
        var ticks = created.UploadedAt.UtcTicks.ToString(CultureInfo.InvariantCulture);
        etag.Tag.Should().Be($"\"{created.Id:N}-{ticks}\"");

        using var conditional = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/files/{created.Id}/content"
        );
        conditional.Headers.IfNoneMatch.Add(etag);

        using var response = await client.SendAsync(conditional, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
        var body = await response.Content.ReadAsByteArrayAsync(Ct);
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_AsAdmin_ReplacesContentAndNameKeepingId()
    {
        var created = await UploadAsAdminAsync(fileName: "old.png");
        var client = await LoginAsAdminAsync();

        using var response = await client.SendUploadAsync(
            HttpMethod.Put,
            $"/api/files/{created.Id}",
            TestSeedData.ValidPng(),
            "new.png"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<FileResponse>(Ct);
        updated!.Id.Should().Be(created.Id);
        updated.Name.Should().Be("new.png");

        var stored = await FindAsync<FileEntity>(created.Id);
        stored!.Name.Should().Be("new.png");
        stored.Extension.Should().Be("png");
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesFileAndSubsequentGetIsNotFound()
    {
        var created = await UploadAsAdminAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/files/{created.Id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<FileEntity>(created.Id);
        stored.Should().BeNull();

        using var followUp = await CreateClient()
            .GetAsync(TestUri.Rel($"/api/files/{created.Id}"), Ct);
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

        var response = await client.DeleteWithCsrfAsync($"/api/files/{created.Id}", Ct);

        await response.ShouldBeConflictAsync(ErrorCode.FileInUse);
        var stored = await FindAsync<FileEntity>(created.Id);
        stored
            .Should()
            .NotBeNull("a file embedded in a rich-text description must survive deletion");
    }
}
