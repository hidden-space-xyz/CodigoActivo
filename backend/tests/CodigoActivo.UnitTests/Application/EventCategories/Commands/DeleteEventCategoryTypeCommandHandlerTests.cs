using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
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
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteEventCategoryTypeCommandHandler sut;

    public DeleteEventCategoryTypeCommandHandlerTests()
    {
        sut = new DeleteEventCategoryTypeCommandHandler(
            categoryTypes,
            events,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncNothingRemovedReturnsNotFound()
    {
        categoryTypes.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteEventCategoryTypeCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeOnlyCategoryOfEvent);
        categoryTypes.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncRemovedInvalidatesCategoryTypesAndEventsCache()
    {
        var categoryType = EventCategoryType.Create("Talleres", "#112233");
        categoryTypes.Finds(categoryType);

        var result = await sut.HandleAsync(
            new DeleteEventCategoryTypeCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        categoryTypes.Received(1).Remove(categoryType);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null
                    && tags.Contains(CacheTags.EventCategoryTypes)
                    && tags.Contains(CacheTags.Events)
                )
            );
    }
}
