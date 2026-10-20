using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.EventCategories;

public sealed class EventCategoryTypeTests
{
    [Fact]
    public void EventCategoryTypeCreateThenRenameTrimsNameAndColor()
    {
        var categoryType = EventCategoryType.Create("  Talleres ", " #112233 ");

        categoryType.Name.Should().Be("Talleres");
        categoryType.Color.Should().Be("#112233");

        categoryType.Rename(" Charlas", "#445566 ");

        categoryType.Name.Should().Be("Charlas");
        categoryType.Color.Should().Be("#445566");
    }

    [Fact]
    public void EventCategoryTypeCreateStableIdKeepsIt()
    {
        var id = Guid.NewGuid();

        EventCategoryType
            .Create("Talleres", "#112233", EventCategoryTypeId.From(id))
            .Id.Value.Should()
            .Be(id);
    }
}
