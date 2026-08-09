using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class UpdateEventCategoryTypeCommandHandlerTests
{
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateEventCategoryTypeCommandHandler sut;

    public UpdateEventCategoryTypeCommandHandlerTests()
    {
        sut = new UpdateEventCategoryTypeCommandHandler(categoryTypes, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncTypeMissingReturnsNotFound()
    {
        categoryTypes.Finds(null);

        var result = await sut.HandleAsync(
            new UpdateEventCategoryTypeCommand(
                Guid.NewGuid(),
                new UpdateEventCategoryTypeRequest("Talleres", "#112233")
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncNameTakenByAnotherReturnsConflict()
    {
        var id = Guid.NewGuid();
        var existing = new EventCategoryType
        {
            Id = id,
            Name = "Old",
            Color = "#000000",
        };
        categoryTypes.Finds(existing);
        categoryTypes.CategoryTypeNameTaken(true);

        var result = await sut.HandleAsync(
            new UpdateEventCategoryTypeCommand(
                id,
                new UpdateEventCategoryTypeRequest("Talleres", "#112233")
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNameAlreadyExists);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestMutatesPersistsAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var existing = new EventCategoryType
        {
            Id = id,
            Name = "Old",
            Color = "#000000",
        };
        categoryTypes.Finds(existing);
        categoryTypes.CategoryTypeNameTaken(false);

        var result = await sut.HandleAsync(
            new UpdateEventCategoryTypeCommand(
                id,
                new UpdateEventCategoryTypeRequest("  New  ", "  #abcdef  ")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New");
        result.Value.Color.Should().Be("#abcdef");
        existing.Name.Should().Be("New");
        existing.Color.Should().Be("#abcdef");
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
