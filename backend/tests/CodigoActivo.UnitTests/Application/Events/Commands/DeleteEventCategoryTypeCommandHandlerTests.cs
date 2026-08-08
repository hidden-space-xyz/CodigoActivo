using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class DeleteEventCategoryTypeCommandHandlerTests
{
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteEventCategoryTypeCommandHandler sut;

    public DeleteEventCategoryTypeCommandHandlerTests()
    {
        sut = new DeleteEventCategoryTypeCommandHandler(categoryTypes, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsync_NothingRemoved_ReturnsNotFound()
    {
        categoryTypes
            .RemoveAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(0);

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
    public async Task HandleAsync_Removed_InvalidatesCategoryTypesAndEventsCache()
    {
        categoryTypes
            .RemoveAsync(
                Arg.Any<Expression<Func<EventCategoryType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1);

        var result = await sut.HandleAsync(
            new DeleteEventCategoryTypeCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
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
