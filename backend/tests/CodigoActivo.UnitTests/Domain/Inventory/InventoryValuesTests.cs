using AwesomeAssertions;
using CodigoActivo.Domain.Inventory;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Inventory;

public sealed class InventoryValuesTests
{
    private static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void FromStoredJsonReadsEachValueByColumn()
    {
        var values = InventoryValues.FromStored(
            $$"""{"{{Second}}": "Batería\nnueva", "{{First}}": "ñandú"}"""
        );

        values.Of(InventoryColumnId.From(First)).Should().Be("ñandú");
        values.Of(InventoryColumnId.From(Second)).Should().Be("Batería\nnueva");
        values.Of(InventoryColumnId.New()).Should().BeNull();
        InventoryValues.FromStored(values.Json).Should().Be(values);
    }

    [Fact]
    public void EqualsSameValuesIgnoresTheirStoredLayout()
    {
        var spaced = InventoryValues.FromStored($$"""{ "{{First}}" : "a", "{{Second}}" : "b" }""");
        var compact = InventoryValues.FromStored($$"""{"{{Second}}":"b","{{First}}":"a"}""");
        var other = InventoryValues.FromStored($$"""{"{{First}}":"a"}""");

        spaced.Should().Be(compact);
        spaced.GetHashCode().Should().Be(compact.GetHashCode());
        spaced.Equals((object)compact).Should().BeTrue();
        spaced.Should().NotBe(other);
        spaced.Equals(null).Should().BeFalse();
        InventoryValues.Empty.Json.Should().Be("{}");
    }
}
