using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class NewsControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Description = "{}";

    private async Task<Guid> SeedNewsItemAsync(
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
            db.News.Add(
                new NewsItem
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
    public async Task ListNoFiltersReturnsOkPagedEnvelope()
    {
        await SeedNewsItemAsync("Alpha");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/news"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<NewsListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.Items.Should().ContainSingle(a => a.Title == "Alpha");
    }

    [Fact]
    public async Task YearsDuplicateYearsReturnsDistinctDescending()
    {
        await SeedNewsItemAsync("A", year: 2021);
        await SeedNewsItemAsync("B", year: 2023);
        await SeedNewsItemAsync("C", year: 2021);
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/news/years"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var years = await response.ReadJsonAsync<IReadOnlyList<int>>(Ct);
        years.Should().Equal(2023, 2021);
    }

    [Fact]
    public async Task GetNewsItemExistsReturnsOkWithNewsItem()
    {
        var id = await SeedNewsItemAsync("Beta");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/news/{id}"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newsItem = await response.ReadJsonAsync<NewsItemResponse>(Ct);
        newsItem!.Title.Should().Be("Beta");
    }

    [Fact]
    public async Task CreateAsAdminReturnsCreatedAndPersists()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateNewsItemRequest("Gamma", "Tagline", Description, thumbnailId);

        var response = await client.PostJsonAsync("/api/news", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<NewsItemResponse>(Ct);
        created!.Title.Should().Be("Gamma");

        var stored = await FindAsync<NewsItem>(created.Id);
        stored!.Subtitle.Should().Be("Tagline");
        stored.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        stored.Featured.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsMemberReturnsForbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsMemberAsync();
        var request = new CreateNewsItemRequest("Nope", "Sub", Description, thumbnailId);

        var response = await client.PostJsonAsync("/api/news", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();
        var request = new CreateNewsItemRequest("Nope", "Sub", Description, Guid.NewGuid());

        var response = await client.PostJsonAsync("/api/news", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("   ", "Sub")]
    [InlineData("Title", "   ")]
    public async Task CreateBlankFieldReturnsValidationError(string title, string subtitle)
    {
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new CreateNewsItemRequest(title, subtitle, Description, thumbnailId);

        var response = await client.PostJsonAsync("/api/news", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task CreateMissingCsrfTokenReturnsBadRequest()
    {
        var client = await LoginAsAdminAsync();
        var thumbnailId = await SeedThumbnailAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/news")
        {
            Content = JsonContent.Create(
                new CreateNewsItemRequest("Gamma", "Sub", Description, thumbnailId),
                options: TestJson.Options
            ),
        };

        var response = await client.SendAsync(request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);
    }

    [Fact]
    public async Task UpdateAsAdminPersistsChanges()
    {
        var id = await SeedNewsItemAsync("Before");
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateNewsItemRequest("After", "NewSub", Description, thumbnailId);

        var response = await client.PutJsonAsync($"/api/news/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<NewsItem>(id);
        stored!.Title.Should().Be("After");
        stored.Subtitle.Should().Be("NewSub");
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task UpdateReplacesThumbnailDeletesOrphanedOldFile()
    {
        var id = await SeedNewsItemAsync("Reemplazo");
        var oldThumbnailId = (await FindAsync<NewsItem>(id))!.ThumbnailId;
        var newThumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = new UpdateNewsItemRequest("Reemplazo", "Sub", Description, newThumbnailId);

        var response = await client.PutJsonAsync($"/api/news/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldFile = await FindAsync<FileEntity>(oldThumbnailId);
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await FindAsync<FileEntity>(newThumbnailId);
        newFile.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateRemovesEmbeddedImageDeletesOrphanedFile()
    {
        var id = await SeedNewsItemAsync("Con imagen");
        var thumbnailId = (await FindAsync<NewsItem>(id))!.ThumbnailId;
        var embeddedFileId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var withImage = new UpdateNewsItemRequest(
            "Con imagen",
            "Sub",
            $"{{\"img\":\"/api/files/{embeddedFileId}/content\"}}",
            thumbnailId
        );
        using (var seeded = await client.PutJsonAsync($"/api/news/{id}", withImage, Ct))
        {
            seeded.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var withoutImage = new UpdateNewsItemRequest("Con imagen", "Sub", Description, thumbnailId);

        var response = await client.PutJsonAsync($"/api/news/{id}", withoutImage, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var file = await FindAsync<FileEntity>(embeddedFileId);
        file.Should()
            .BeNull(
                "an image dropped from the description is orphaned and must be cascade-deleted"
            );
    }

    [Fact]
    public async Task FeatureNewsItemMissingReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync($"/api/news/{Guid.NewGuid()}/feature", ct: Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.NewsItemNotFound);
    }

    [Fact]
    public async Task DeleteAsAdminRemovesNewsItemAndOrphanedThumbnail()
    {
        var id = await SeedNewsItemAsync("Doomed");
        var thumbnailId = (await FindAsync<NewsItem>(id))!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/news/{id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<NewsItem>(id);
        stored.Should().BeNull();
        var file = await FindAsync<FileEntity>(thumbnailId);
        file.Should()
            .BeNull("the deleted news item's thumbnail is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task DeleteSharedThumbnailKeepsThumbnailAndSurvivor()
    {
        var sharedThumbnailId = await SeedThumbnailAsync();
        var doomedId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.News.AddRange(
                NewSharedThumbnailNewsItem(doomedId, "Doomed", sharedThumbnailId),
                NewSharedThumbnailNewsItem(survivorId, "Survivor", sharedThumbnailId)
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/news/{doomedId}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var file = await FindAsync<FileEntity>(sharedThumbnailId);
        file.Should()
            .NotBeNull("a thumbnail still referenced by another entity must survive the cascade");
        var survivor = await FindAsync<NewsItem>(survivorId);
        survivor.Should().NotBeNull();
    }

    private static NewsItem NewSharedThumbnailNewsItem(Guid id, string title, Guid thumbnailId)
    {
        return new()
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
}
