using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Inventory.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Inventory;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Inventory.InventoryTestData;

namespace CodigoActivo.UnitTests.Application.Inventory.Commands;

public sealed class DeleteInventoryEntryCommandHandlerTests
{
    private readonly IInventoryCategoryRepository categories =
        Substitute.For<IInventoryCategoryRepository>();
    private readonly DeleteInventoryEntryCommandHandler sut;

    public DeleteInventoryEntryCommandHandlerTests()
    {
        sut = new DeleteInventoryEntryCommandHandler(categories);
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        categories.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteInventoryEntryCommand(InventoryCategoryId.New(), InventoryEntryId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
    }

    [Fact]
    public async Task HandleAsyncEntryFoundRemovesIt()
    {
        var category = Robots();
        var entryId = category
            .AddEntry(
                1,
                new Dictionary<InventoryColumnId, string> { [category.Tipo().Id] = "mBot" }
            )
            .Value;
        categories.Finds(category);

        var result = await sut.HandleAsync(
            new DeleteInventoryEntryCommand(category.Id, entryId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        category.Entries.Should().BeEmpty();
    }
}
