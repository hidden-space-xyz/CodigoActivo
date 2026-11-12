using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Inventory.Contracts;
using CodigoActivo.Application.Inventory.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Inventory.InventoryRowData;

namespace CodigoActivo.UnitTests.Application.Inventory.Queries;

public sealed class ListInventoryCategoriesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListInventoryCategoriesQueryHandler sut;

    public ListInventoryCategoriesQueryHandlerTests()
    {
        sut = new ListInventoryCategoriesQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncCategoriesAddUpTheQuantityOrCountTheirEntries()
    {
        var shirts = CategoryRow(
            "Camisetas",
            [
                ColumnRow("Talla", InventoryColumnType.Text, 0),
                ColumnRow("Cantidad", InventoryColumnType.Quantity, 1),
            ],
            EntryRow(30),
            EntryRow(),
            EntryRow(12)
        );
        var laptops = CategoryRow(
            "Portátiles",
            [ColumnRow("Nombre", InventoryColumnType.Text, 0)],
            EntryRow(),
            EntryRow()
        );
        store.InventoryCategories.AddRange([laptops, shirts]);

        var page = await sut.HandleAsync(
            new ListInventoryCategoriesQuery(new InventoryCategoryListQuery { Sort = "-total" }),
            TestContext.Current.CancellationToken
        );

        page.Total.Should().Be(2);
        page.Items.Should()
            .Equal(
                new InventoryCategorySummaryResponse(shirts.Id, "Camisetas", 3, 42),
                new InventoryCategorySummaryResponse(laptops.Id, "Portátiles", 2, 2)
            );
    }

    [Fact]
    public async Task HandleAsyncNameFilterKeepsTheMatchingCategories()
    {
        store.InventoryCategories.AddRange([
            CategoryRow("Robots", [ColumnRow("Tipo", InventoryColumnType.Text, 0)]),
            CategoryRow("Portátiles", [ColumnRow("Nombre", InventoryColumnType.Text, 0)]),
        ]);

        var page = await sut.HandleAsync(
            new ListInventoryCategoriesQuery(new InventoryCategoryListQuery { Name = "portat" }),
            TestContext.Current.CancellationToken
        );

        page.Items.Should().ContainSingle().Which.Name.Should().Be("Portátiles");
    }
}
