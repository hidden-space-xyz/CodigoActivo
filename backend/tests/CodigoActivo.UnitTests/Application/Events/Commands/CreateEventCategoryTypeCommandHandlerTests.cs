using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class CreateEventCategoryTypeCommandHandlerTests
{
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateEventCategoryTypeCommandHandler sut;

    public CreateEventCategoryTypeCommandHandlerTests()
    {
        sut = new CreateEventCategoryTypeCommandHandler(categoryTypes, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncNameExistsReturnsConflict()
    {
        categoryTypes.CategoryTypeNameTaken(true);

        var result = await sut.HandleAsync(
            new CreateEventCategoryTypeCommand(
                new CreateEventCategoryTypeRequest("  Talleres  ", "  #112233  ")
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.EventCategoryTypeNameAlreadyExists);
        await categoryTypes
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventCategoryType>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestPersistsTrimmedTypeAndInvalidatesCache()
    {
        categoryTypes.CategoryTypeNameTaken(false);

        var result = await sut.HandleAsync(
            new CreateEventCategoryTypeCommand(
                new CreateEventCategoryTypeRequest("  Talleres  ", "  #112233  ")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Talleres");
        result.Value.Color.Should().Be("#112233");
        await categoryTypes
            .Received(1)
            .AddAsync(
                Arg.Is<EventCategoryType>(c =>
                    c != null && c.Name == "Talleres" && c.Color == "#112233"
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.EventCategoryTypes)
                )
            );
    }
}
