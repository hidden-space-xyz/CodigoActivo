using AwesomeAssertions;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Announcements.AnnouncementTestData;

namespace CodigoActivo.UnitTests.Application.Announcements.Queries;

public sealed class GetAnnouncementYearsQueryHandlerTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly GetAnnouncementYearsQueryHandler sut;

    public GetAnnouncementYearsQueryHandlerTests()
    {
        sut = new GetAnnouncementYearsQueryHandler(
            announcements,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_DuplicateYears_ReturnsDistinctDescending()
    {
        announcements.HasAnnouncements(
            NewAnnouncement(year: 2023),
            NewAnnouncement(year: 2025),
            NewAnnouncement(year: 2023),
            NewAnnouncement(year: 2024)
        );

        var result = await sut.HandleAsync(
            new GetAnnouncementYearsQuery(),
            TestContext.Current.CancellationToken
        );

        result.Should().ContainInOrder(2025, 2024, 2023);
        result.Should().HaveCount(3);
    }
}
