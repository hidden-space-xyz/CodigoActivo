using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Inventory.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Inventory;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Inventory.InventoryRowData;
using InventoryColumnType = CodigoActivo.Application.Abstractions.Querying.ReadModel.InventoryColumnType;

namespace CodigoActivo.UnitTests.Application.Inventory.Queries;

public sealed class ListInventoryEntriesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListInventoryEntriesQueryHandler sut;

    public ListInventoryEntriesQueryHandlerTests()
    {
        sut = new ListInventoryEntriesQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncCategoryExistsReturnsItsEntriesWithTheirValues()
    {
        var column = Guid.NewGuid();
        var category = CategoryRow(
            "Robots",
            [ColumnRow("Tipo", InventoryColumnType.Text, 0)],
            EntryRow(3, $$"""{"{{column}}": "mBot"}""")
        );
        var other = CategoryRow(
            "Kits",
            [ColumnRow("Tipo", InventoryColumnType.Text, 0)],
            EntryRow()
        );
        store.InventoryCategories.AddRange([category, other]);
        store.InventoryEntries.AddRange([.. category.Entries, .. other.Entries]);

        var result = await sut.HandleAsync(
            new ListInventoryEntriesQuery(InventoryCategoryId.From(category.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var entry = result.Value.Should().ContainSingle().Which;
        entry.Id.Should().Be(category.Entries.Single().Id);
        entry.Quantity.Should().Be(3);
        entry.Values.Should().Equal(new Dictionary<Guid, string> { [column] = "mBot" });
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new ListInventoryEntriesQuery(InventoryCategoryId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
    }
}
