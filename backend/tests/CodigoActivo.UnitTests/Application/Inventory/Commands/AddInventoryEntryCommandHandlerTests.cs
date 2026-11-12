using AwesomeAssertions;
using CodigoActivo.API.Inventory.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Inventory.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Inventory;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Inventory.InventoryTestData;

namespace CodigoActivo.UnitTests.Application.Inventory.Commands;

public sealed class AddInventoryEntryCommandHandlerTests
{
    private readonly IInventoryCategoryRepository categories =
        Substitute.For<IInventoryCategoryRepository>();
    private readonly AddInventoryEntryCommandHandler sut;

    public AddInventoryEntryCommandHandlerTests()
    {
        sut = new AddInventoryEntryCommandHandler(categories);
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        categories.Finds(null);

        var result = await sut.HandleAsync(
            new AddInventoryEntryRequest(1, new Dictionary<Guid, string>()).ToCommand(
                InventoryCategoryId.New()
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestAddsTheEntry()
    {
        var category = Robots();
        categories.Finds(category);

        var result = await sut.HandleAsync(
            new AddInventoryEntryRequest(
                12,
                new Dictionary<Guid, string> { [category.Tipo().Id.Value] = " mBot " }
            ).ToCommand(category.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var entry = category.Entries.Should().ContainSingle().Which;
        entry.Id.Should().Be(result.Value);
        entry.Quantity.Should().Be(12);
        entry.Values.Of(category.Tipo().Id).Should().Be("mBot");
    }

    [Fact]
    public async Task HandleAsyncValuesBreakingARuleReturnsTheFailure()
    {
        var category = Robots();
        categories.Finds(category);

        var result = await sut.HandleAsync(
            new AddInventoryEntryRequest(3, new Dictionary<Guid, string>()).ToCommand(category.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueRequired);
        category.Entries.Should().BeEmpty();
    }
}
