using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

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

    private EventCategoryType FindsCategory()
    {
        var categoryType = EventCategoryType.Create("Talleres", "#112233");
        categoryType.PullDomainEvents();
        categoryTypes.Finds(categoryType);
        return categoryType;
    }

    private void Tags(EventCategoryType categoryType, params Event[] tagged)
    {
        events.ListTaggedWithAsync(categoryType.Id, Arg.Any<CancellationToken>()).Returns(tagged);
    }

    private Task<Result> DeleteAsync(EventCategoryTypeId id)
    {
        return sut.HandleAsync(
            new DeleteEventCategoryTypeCommand(id),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncNothingRemovedReturnsNotFound()
    {
        categoryTypes.Finds(null);

        var result = await DeleteAsync(EventCategoryTypeId.New());

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventCategoryTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncOnlyCategoryOfSomeEventReturnsConflict()
    {
        var categoryType = FindsCategory();
        var other = EventCategoryTypeId.New();
        var shared = NewEvent(categoryTypeIds: [categoryType.Id, other]);
        Tags(categoryType, shared, NewEvent(categoryTypeIds: [categoryType.Id]));

        var result = await DeleteAsync(categoryType.Id);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.EventCategoryTypeOnlyCategoryOfEvent);
        categoryTypes.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task HandleAsyncTaggedEventsLoseItAndItIsRemoved()
    {
        var categoryType = FindsCategory();
        var other = EventCategoryTypeId.New();
        var tagged = NewEvent(categoryTypeIds: [categoryType.Id, other]);
        Tags(categoryType, tagged);

        var result = await DeleteAsync(categoryType.Id);

        result.IsSuccess.Should().BeTrue();
        tagged.Categories.Select(category => category.EventCategoryTypeId).Should().Equal(other);
        categoryType
            .PullDomainEvents()
            .Should()
            .Equal(new EventCategoryTypeDeleted(categoryType.Id));
        categoryTypes.Received(1).Remove(categoryType);
    }
}
