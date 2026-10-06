using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Application.EventCategories.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.EventCategories.Queries;

public sealed class GetEventCategoryTypeByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetEventCategoryTypeByIdQueryHandler sut;

    public GetEventCategoryTypeByIdQueryHandlerTests()
    {
        sut = new GetEventCategoryTypeByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncCategoryTypeExistsReturnsCategoryType()
    {
        var categoryType = NewCategoryTypeRow("Talleres", "#AABB11");
        store.EventCategoryTypes.AddRange([NewCategoryTypeRow("Charlas"), categoryType]);

        var result = await sut.HandleAsync(
            new GetEventCategoryTypeByIdQuery(EventCategoryTypeId.From(categoryType.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new EventCategoryTypeResponse(categoryType.Id, "Talleres", "#AABB11"));
    }

    [Fact]
    public async Task HandleAsyncCategoryTypeMissingReturnsNotFound()
    {
        store.EventCategoryTypes.Add(NewCategoryTypeRow("Charlas"));

        var result = await sut.HandleAsync(
            new GetEventCategoryTypeByIdQuery(EventCategoryTypeId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventCategoryTypeNotFound);
    }
}
