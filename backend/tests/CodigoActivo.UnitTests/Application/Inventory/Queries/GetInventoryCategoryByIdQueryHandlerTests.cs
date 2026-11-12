using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Inventory.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Inventory;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Inventory.InventoryRowData;
using InventoryColumnType = CodigoActivo.Application.Abstractions.Querying.ReadModel.InventoryColumnType;

namespace CodigoActivo.UnitTests.Application.Inventory.Queries;

public sealed class GetInventoryCategoryByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetInventoryCategoryByIdQueryHandler sut;

    public GetInventoryCategoryByIdQueryHandlerTests()
    {
        sut = new GetInventoryCategoryByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncCategoryExistsReturnsItsColumnsInOrder()
    {
        var category = CategoryRow(
            "Camisetas",
            [
                ColumnRow("Cantidad", InventoryColumnType.Quantity, 1),
                ColumnRow(
                    "Talla",
                    InventoryColumnType.List,
                    0,
                    OptionRow("M", 1),
                    OptionRow("S", 0)
                ),
            ],
            EntryRow(5)
        );
        store.InventoryCategories.Add(category);

        var result = await sut.HandleAsync(
            new GetInventoryCategoryByIdQuery(InventoryCategoryId.From(category.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Camisetas");
        result.Value.Total.Should().Be(5);
        result.Value.EntryCount.Should().Be(1);
        result.Value.Columns.Select(column => column.Name).Should().Equal("Talla", "Cantidad");
        result.Value.Columns[0].Options.Select(option => option.Name).Should().Equal("S", "M");
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetInventoryCategoryByIdQuery(InventoryCategoryId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
    }
}
