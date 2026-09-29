using AwesomeAssertions;
using CodigoActivo.Application.Reports;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetDashboardSummaryQueryHandlerTests
{
    private readonly IDashboardCountsReader dashboard = Substitute.For<IDashboardCountsReader>();
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
                    News = 4,
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
