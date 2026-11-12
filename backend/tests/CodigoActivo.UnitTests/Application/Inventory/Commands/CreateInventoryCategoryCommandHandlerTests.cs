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
using InventoryColumnType = CodigoActivo.Application.Abstractions.Querying.ReadModel.InventoryColumnType;

namespace CodigoActivo.UnitTests.Application.Inventory.Commands;

public sealed class CreateInventoryCategoryCommandHandlerTests
{
    private readonly IInventoryCategoryRepository categories =
        Substitute.For<IInventoryCategoryRepository>();
    private readonly CreateInventoryCategoryCommandHandler sut;

    public CreateInventoryCategoryCommandHandlerTests()
    {
        sut = new CreateInventoryCategoryCommandHandler(categories);
    }

    [Fact]
    public async Task HandleAsyncValidRequestAddsTheCategoryWithItsColumns()
    {
        var request = new CreateInventoryCategoryRequest(
            " Camisetas ",
            [
                new InventoryColumnRequest(
                    null,
                    "Talla",
                    InventoryColumnType.List,
                    true,
                    [new InventoryColumnOptionRequest(null, "M", "#112233")]
                ),
                QuantityColumn(),
            ]
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await categories
            .Received(1)
            .NameExistsAsync("Camisetas", null, Arg.Any<CancellationToken>());
        await categories
            .Received(1)
            .AddAsync(
                Arg.Is<InventoryCategory>(category =>
                    category.Id == result.Value
                    && category.Name == "Camisetas"
                    && category.Columns.Count == 2
                    && category.Columns.Single(column => column.Name == "Talla").Options.Count == 1
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncNameTakenReturnsConflict()
    {
        categories.NameExistsAsync("Robots", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await sut.HandleAsync(
            new CreateInventoryCategoryRequest("Robots", [TextColumn("Tipo")]).ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(
            ErrorKind.Conflict,
            ApplicationErrorCode.InventoryCategoryNameAlreadyExists
        );
        await categories
            .DidNotReceive()
            .AddAsync(Arg.Any<InventoryCategory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncColumnsBreakingARuleReturnsTheFailure()
    {
        var result = await sut.HandleAsync(
            new CreateInventoryCategoryRequest(
                "Robots",
                [QuantityColumn(), QuantityColumn("Repuestos")]
            ).ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryQuantityColumnsExceeded);
        await categories
            .DidNotReceive()
            .AddAsync(Arg.Any<InventoryCategory>(), Arg.Any<CancellationToken>());
    }
}
