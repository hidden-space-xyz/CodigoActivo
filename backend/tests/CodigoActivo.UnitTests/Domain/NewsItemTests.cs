using AwesomeAssertions;
using CodigoActivo.Domain.News;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class NewsItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void CreateContentWithSpacesStoresTrimmedTitlesUnfeaturedAndAuthor()
    {
        var authorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();

        var newsItem = NewsItem.Create(
            new NewsItemContent("  Title ", " Sub  ", "{\"a\":1}", thumbnailId),
            authorId,
            Now
        );

        newsItem.Title.Should().Be("Title");
        newsItem.Subtitle.Should().Be("Sub");
        newsItem.Description.Should().Be("{\"a\":1}");
        newsItem.ThumbnailId.Should().Be(thumbnailId);
        newsItem.Featured.Should().BeFalse();
        newsItem.CreatedBy.Should().Be(authorId);
        newsItem.CreatedAt.Should().Be(Now);
        newsItem.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateNewContentReplacesItAndRecordsEditor()
    {
        var newsItem = NewsItem.Create(
            new NewsItemContent("Old", "Old", "{}", Guid.NewGuid()),
            Guid.NewGuid(),
            Now
        );
        var editorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();

        newsItem.Update(
            new NewsItemContent(" New ", " Sub ", "{\"b\":2}", thumbnailId),
            editorId,
            Now.AddHours(1)
        );

        newsItem.Title.Should().Be("New");
        newsItem.Subtitle.Should().Be("Sub");
        newsItem.Description.Should().Be("{\"b\":2}");
        newsItem.ThumbnailId.Should().Be(thumbnailId);
        newsItem.UpdatedBy.Should().Be(editorId);
        newsItem.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void FeatureThenUnfeatureTogglesTheFlagWithoutAnEdit()
    {
        var newsItem = NewsItem.Create(
            new NewsItemContent("T", "S", "{}", Guid.NewGuid()),
            Guid.NewGuid(),
            Now
        );

        newsItem.Feature();
        newsItem.Featured.Should().BeTrue();
        newsItem.Unfeature();

        newsItem.Featured.Should().BeFalse();
        newsItem.UpdatedAt.Should().BeNull();
        newsItem.UpdatedBy.Should().BeNull();
    }
}
