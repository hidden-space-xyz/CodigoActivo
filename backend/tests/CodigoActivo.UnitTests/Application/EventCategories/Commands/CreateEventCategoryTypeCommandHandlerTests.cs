using AwesomeAssertions;
using CodigoActivo.API.EventCategories.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
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
    private readonly CreateEventCategoryTypeCommandHandler sut;

    public CreateEventCategoryTypeCommandHandlerTests()
    {
        sut = new CreateEventCategoryTypeCommandHandler(categoryTypes);
    }

    [Fact]
    public async Task HandleAsyncNameExistsReturnsConflict()
    {
        categoryTypes.CategoryTypeNameTaken(true);

        var result = await sut.HandleAsync(
            new CreateEventCategoryTypeRequest("  Talleres  ", "  #112233  ").ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventCategoryTypeNameAlreadyExists);
        await categoryTypes
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventCategoryType>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestStagesTrimmedType()
    {
        categoryTypes.CategoryTypeNameTaken(false);
        var added = new List<EventCategoryType>();
        await categoryTypes.AddAsync(
            Arg.Do<EventCategoryType>(added.Add),
            Arg.Any<CancellationToken>()
        );

        var result = await sut.HandleAsync(
            new CreateEventCategoryTypeRequest("  Talleres  ", "  #112233  ").ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Name.Should().Be("Talleres");
        created.Color.Should().Be("#112233");
        created.PullDomainEvents().Should().Equal(new EventCategoryTypeCreated(created.Id));
    }
}
