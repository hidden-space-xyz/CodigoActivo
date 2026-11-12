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

public sealed class UpdateInventoryEntryCommandHandlerTests
{
    private readonly IInventoryCategoryRepository categories =
        Substitute.For<IInventoryCategoryRepository>();
    private readonly UpdateInventoryEntryCommandHandler sut;

    public UpdateInventoryEntryCommandHandlerTests()
    {
        sut = new UpdateInventoryEntryCommandHandler(categories);
    }

    [Fact]
    public async Task HandleAsyncCategoryMissingReturnsNotFound()
    {
        categories.Finds(null);

        var result = await sut.HandleAsync(
            new UpdateInventoryEntryRequest(1, new Dictionary<Guid, string>()).ToCommand(
                InventoryCategoryId.New(),
                InventoryEntryId.New()
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.InventoryCategoryNotFound);
    }

    [Fact]
    public async Task HandleAsyncEntryMissingReturnsNotFound()
    {
        var category = Robots();
        categories.Finds(category);

        var result = await sut.HandleAsync(
            new UpdateInventoryEntryRequest(1, new Dictionary<Guid, string>()).ToCommand(
                category.Id,
                InventoryEntryId.New()
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, DomainErrorCode.InventoryEntryNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestReplacesTheValues()
    {
        var category = Robots();
        var tipo = category.Tipo();
        var entryId = category
            .AddEntry(2, new Dictionary<InventoryColumnId, string> { [tipo.Id] = "mBot" })
            .Value;
        categories.Finds(category);

        var result = await sut.HandleAsync(
            new UpdateInventoryEntryRequest(
                5,
                new Dictionary<Guid, string> { [tipo.Id.Value] = "Spike" }
            ).ToCommand(category.Id, entryId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var entry = category.Entries.Single();
        entry.Quantity.Should().Be(5);
        entry.Values.Of(tipo.Id).Should().Be("Spike");
    }
}
