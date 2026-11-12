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

public sealed class DeleteInventoryCategoryCommandHandlerTests
{
    private readonly IInventoryCategoryRepository categories =
        Substitute.For<IInventoryCategoryRepository>();
    private readonly DeleteInventoryCategoryCommandHandler sut;

    public DeleteInventoryCategoryCommandHandlerTests()
    {
        sut = new DeleteInventoryCategoryCommandHandler(categories);
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        categories.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteInventoryCategoryCommand(InventoryCategoryId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
        categories.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task HandleAsyncCategoryFoundRemovesIt()
    {
        var category = Robots();
        categories.Finds(category);

        var result = await sut.HandleAsync(
            new DeleteInventoryCategoryCommand(category.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        categories.Received(1).Remove(category);
    }
}
