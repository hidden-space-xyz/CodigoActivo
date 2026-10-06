using AwesomeAssertions;
using CodigoActivo.API.EventCategories.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.EventCategories.Commands;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.UnitTests.Application.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.EventCategories.Commands;

public sealed class UpdateEventCategoryTypeCommandHandlerTests
{
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly UpdateEventCategoryTypeCommandHandler sut;

    public UpdateEventCategoryTypeCommandHandlerTests()
    {
        sut = new UpdateEventCategoryTypeCommandHandler(categoryTypes);
    }

    [Fact]
    public async Task HandleAsyncTypeMissingReturnsNotFound()
    {
        categoryTypes.Finds(null);

        var result = await sut.HandleAsync(
            new UpdateEventCategoryTypeRequest("Talleres", "#112233").ToCommand(
                EventCategoryTypeId.New()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventCategoryTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncNameTakenByAnotherReturnsConflict()
    {
        var id = Guid.NewGuid();
        var existing = Persisted.As<EventCategoryType>(
            new
            {
                Id = id,
                Name = "Old",
                Color = "#000000",
            }
        );
        categoryTypes.Finds(existing);
        categoryTypes.CategoryTypeNameTaken(true);

        var result = await sut.HandleAsync(
            new UpdateEventCategoryTypeRequest("Talleres", "#112233").ToCommand(existing.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventCategoryTypeNameAlreadyExists);
    }

    [Fact]
    public async Task HandleAsyncValidRequestRenamesTheCategoryType()
    {
        var id = Guid.NewGuid();
        var existing = Persisted.As<EventCategoryType>(
            new
            {
                Id = id,
                Name = "Old",
                Color = "#000000",
            }
        );
        categoryTypes.Finds(existing);
        categoryTypes.CategoryTypeNameTaken(false);

        var result = await sut.HandleAsync(
            new UpdateEventCategoryTypeRequest("  New  ", "  #abcdef  ").ToCommand(existing.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        existing.Name.Should().Be("New");
        existing.Color.Should().Be("#abcdef");
        existing.PullDomainEvents().Should().Equal(new EventCategoryTypeRenamed(existing.Id));
    }
}
