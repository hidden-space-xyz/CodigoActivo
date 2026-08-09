using AwesomeAssertions;
using CodigoActivo.Application.Announcements.Commands;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Announcements.AnnouncementTestData;

namespace CodigoActivo.UnitTests.Application.Announcements.Commands;

public sealed class SetAnnouncementFeaturedCommandHandlerTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly SetAnnouncementFeaturedCommandHandler sut;

    public SetAnnouncementFeaturedCommandHandlerTests()
    {
        sut = new SetAnnouncementFeaturedCommandHandler(
            announcements,
            cacheInvalidator,
            new GetAnnouncementByIdQueryHandler(
                announcements,
                new FakeQueryExecutor(),
                new FakeHybridCache()
            )
        );
    }

    [Fact]
    public async Task HandleAsyncIdMissingReturnsNotFound()
    {
        announcements
            .SetFeaturedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.HandleAsync(
            new SetAnnouncementFeaturedCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementNotFound);
    }

    [Fact]
    public async Task HandleAsyncMarkedReturnsFeaturedAnnouncement()
    {
        var announcement = NewAnnouncement("Featured", featured: true);
        announcements.SetFeaturedAsync(announcement.Id, Arg.Any<CancellationToken>()).Returns(true);
        announcements.HasAnnouncements(announcement);

        var result = await sut.HandleAsync(
            new SetAnnouncementFeaturedCommand(announcement.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(announcement.Id);
        result.Value.Featured.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncMarkedInvalidatesAnnouncementsCache()
    {
        var announcement = NewAnnouncement("Featured", featured: true);
        announcements.SetFeaturedAsync(announcement.Id, Arg.Any<CancellationToken>()).Returns(true);
        announcements.HasAnnouncements(announcement);

        var result = await sut.HandleAsync(
            new SetAnnouncementFeaturedCommand(announcement.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Announcements)
                )
            );
    }
}
