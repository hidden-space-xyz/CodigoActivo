using AwesomeAssertions;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Announcements.Queries;

public sealed class GetAnnouncementByIdQueryHandlerTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly GetAnnouncementByIdQueryHandler sut;

    public GetAnnouncementByIdQueryHandlerTests()
    {
        sut = new GetAnnouncementByIdQueryHandler(
            announcements,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncAnnouncementMissingReturnsNotFound()
    {
        announcements.HasAnnouncements();

        var result = await sut.HandleAsync(
            new GetAnnouncementByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }
}
