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

public sealed class UpdateInventoryCategoryCommandHandlerTests
{
    private readonly IInventoryCategoryRepository categories =
        Substitute.For<IInventoryCategoryRepository>();
    private readonly UpdateInventoryCategoryCommandHandler sut;

    public UpdateInventoryCategoryCommandHandlerTests()
    {
        sut = new UpdateInventoryCategoryCommandHandler(categories);
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        categories.Finds(null);

        var result = await sut.HandleAsync(
            new UpdateInventoryCategoryRequest("Robots", [TextColumn("Tipo")]).ToCommand(
                InventoryCategoryId.New()
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
    }

    [Fact]
    public async Task HandleAsyncNameTakenByAnotherReturnsConflict()
    {
        var category = Robots();
        categories.Finds(category);
        categories.NameExistsAsync("Kits", category.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await sut.HandleAsync(
            new UpdateInventoryCategoryRequest("Kits", [TextColumn("Tipo")]).ToCommand(category.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(
            ErrorKind.Conflict,
            ApplicationErrorCode.InventoryCategoryNameAlreadyExists
        );
        category.Name.Should().Be("Robots");
    }

    [Fact]
    public async Task HandleAsyncValidRequestRedefinesTheCategory()
    {
        var category = Robots();
        var tipo = category.Tipo();
        categories.Finds(category);

        var result = await sut.HandleAsync(
            new UpdateInventoryCategoryRequest(
                " Kits ",
                [
                    new InventoryColumnRequest(
                        tipo.Id.Value,
                        "Modelo",
                        InventoryColumnType.Text,
                        true,
                        []
                    ),
                ]
            ).ToCommand(category.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        category.Name.Should().Be("Kits");
        category.Columns.Should().ContainSingle().Which.Name.Should().Be("Modelo");
    }
}
