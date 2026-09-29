using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.News;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class FeaturedSelectionTests
{
    private static NewsItem NewNewsItem(bool featured = false)
    {
        var newsItem = NewsItem.Create(
            new NewsItemContent("T", "S", "{}", Guid.NewGuid()),
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch
        );
        if (featured)
        {
            newsItem.Feature();
        }

        return newsItem;
    }

    [Fact]
    public void ChooseOthersFeaturedLeavesOnlyTheChosenOneFeatured()
    {
        var chosen = NewNewsItem();
        var first = NewNewsItem(featured: true);
        var second = NewNewsItem(featured: true);

        FeaturedSelection.Choose(chosen, [first, second]);

        chosen.Featured.Should().BeTrue();
        first.Featured.Should().BeFalse();
        second.Featured.Should().BeFalse();
    }

    [Fact]
    public void ChooseChosenAlreadyFeaturedKeepsItFeatured()
    {
        var chosen = NewNewsItem(featured: true);

        FeaturedSelection.Choose(chosen, [chosen]);

        chosen.Featured.Should().BeTrue();
    }

    [Fact]
    public void ChooseNothingFeaturedFeaturesTheChosenOne()
    {
        var chosen = NewNewsItem();

        FeaturedSelection.Choose(chosen, []);

        chosen.Featured.Should().BeTrue();
    }
}
