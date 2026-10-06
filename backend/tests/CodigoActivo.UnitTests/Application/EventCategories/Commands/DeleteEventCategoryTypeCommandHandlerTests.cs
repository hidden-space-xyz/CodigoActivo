using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.EventCategories.Commands;

public sealed class DeleteEventCategoryTypeCommandHandlerTests
{
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly DeleteEventCategoryTypeCommandHandler sut;

    public DeleteEventCategoryTypeCommandHandlerTests()
    {
        sut = new DeleteEventCategoryTypeCommandHandler(categoryTypes, events);
    }

    [Fact]
    public async Task HandleAsyncNothingRemovedReturnsNotFound()
    {
        categoryTypes.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteEventCategoryTypeCommand(EventCategoryTypeId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventCategoryTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncOnlyCategoryOfSomeEventReturnsConflict()
    {
        var categoryType = EventCategoryType.Create("Talleres", "#112233");
        categoryTypes.Finds(categoryType);
        events
            .HasEventWithOnlyCategoryAsync(categoryType.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await sut.HandleAsync(
            new DeleteEventCategoryTypeCommand(categoryType.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventCategoryTypeOnlyCategoryOfEvent);
        categoryTypes.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task HandleAsyncFoundRemovesItAndRaisesDeletion()
    {
        var categoryType = EventCategoryType.Create("Talleres", "#112233");
        categoryType.PullDomainEvents();
        categoryTypes.Finds(categoryType);

        var result = await sut.HandleAsync(
            new DeleteEventCategoryTypeCommand(categoryType.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        categoryTypes.Received(1).Remove(categoryType);
        categoryType
            .PullDomainEvents()
            .Should()
            .Equal(new EventCategoryTypeDeleted(categoryType.Id));
    }
}
