using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.UnitTests.Application.Events;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.EventCategories.Commands;

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
        var added = new List<EventCategoryType>();
        await categoryTypes.AddAsync(
            Arg.Do<EventCategoryType>(added.Add),
            Arg.Any<CancellationToken>()
        );

        var result = await sut.HandleAsync(
            new CreateEventCategoryTypeCommand(
                new CreateEventCategoryTypeRequest("  Talleres  ", "  #112233  ")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Name.Should().Be("Talleres");
        created.Color.Should().Be("#112233");
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
