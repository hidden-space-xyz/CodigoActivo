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
    public void UnfeatureAllButOthersFeaturedUnfeaturesThemAndLeavesTheChosenOneAsItWas()
    {
        var chosen = NewNewsItem();
        var first = NewNewsItem(featured: true);
        var second = NewNewsItem(featured: true);

        FeaturedSelection.UnfeatureAllBut(chosen, [first, second]);

        chosen.Featured.Should().BeFalse();
        first.Featured.Should().BeFalse();
        second.Featured.Should().BeFalse();
    }

    [Fact]
    public void UnfeatureAllButChosenAlreadyFeaturedKeepsItFeatured()
    {
        var chosen = NewNewsItem(featured: true);

        FeaturedSelection.UnfeatureAllBut(chosen, [chosen]);

        chosen.Featured.Should().BeTrue();
    }

    [Fact]
    public void UnfeatureAllButNothingFeaturedChangesNothing()
    {
        var chosen = NewNewsItem();

        FeaturedSelection.UnfeatureAllBut(chosen, []);

        chosen.Featured.Should().BeFalse();
    }
}
