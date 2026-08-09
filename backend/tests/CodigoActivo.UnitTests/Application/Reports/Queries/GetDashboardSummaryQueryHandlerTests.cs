using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetDashboardSummaryQueryHandlerTests
{
    private readonly IDashboardRepository dashboard = Substitute.For<IDashboardRepository>();
    private readonly GetDashboardSummaryQueryHandler sut;

    public GetDashboardSummaryQueryHandlerTests()
    {
        sut = new GetDashboardSummaryQueryHandler(dashboard, new FakeHybridCache());
    }

    [Fact]
    public async Task HandleAsyncRepositoryCountsMapsInOrder()
    {
        dashboard
            .GetCountsAsync(Arg.Any<CancellationToken>())
            .Returns(
                new DashboardCounts
                {
                    Events = 1,
                    Activities = 2,
                    Resources = 3,
                    Announcements = 4,
                    Partners = 5,
                    Users = 6,
                }
            );

        var result = await sut.HandleAsync(
            new GetDashboardSummaryQuery(),
            TestContext.Current.CancellationToken
        );

        result.Should().BeEquivalentTo(new DashboardSummaryResponse(1, 2, 3, 4, 5, 6));
    }
}
