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

public sealed class GetInventoryEntryByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetInventoryEntryByIdQueryHandler sut;
    private readonly InventoryCategoryRow category = CategoryRow(
        "Robots",
        [ColumnRow("Tipo", InventoryColumnType.Text, 0)],
        EntryRow(7)
    );

    public GetInventoryEntryByIdQueryHandlerTests()
    {
        sut = new GetInventoryEntryByIdQueryHandler(store, new FakeQueryExecutor());
        store.InventoryCategories.Add(category);
        store.InventoryEntries.AddRange(category.Entries);
    }

    [Fact]
    public async Task HandleAsyncEntryExistsReturnsIt()
    {
        var entry = category.Entries.Single();

        var result = await sut.HandleAsync(
            new GetInventoryEntryByIdQuery(
                InventoryCategoryId.From(category.Id),
                InventoryEntryId.From(entry.Id)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Quantity.Should().Be(7);
        result.Value.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncEntryOfAnotherCategoryReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetInventoryEntryByIdQuery(
                InventoryCategoryId.From(category.Id),
                InventoryEntryId.New()
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, DomainErrorCode.InventoryEntryNotFound);
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetInventoryEntryByIdQuery(
                InventoryCategoryId.New(),
                InventoryEntryId.From(category.Entries.Single().Id)
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
    }
}
