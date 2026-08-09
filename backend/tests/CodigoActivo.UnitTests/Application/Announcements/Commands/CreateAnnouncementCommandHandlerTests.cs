using AwesomeAssertions;
using CodigoActivo.Application.Announcements.Commands;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Announcements.Commands;

public sealed class CreateAnnouncementCommandHandlerTests
{
    private readonly IAnnouncementRepository announcements =
        Substitute.For<IAnnouncementRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateAnnouncementCommandHandler sut;

    public CreateAnnouncementCommandHandlerTests()
    {
        sut = new CreateAnnouncementCommandHandler(
            announcements,
            files,
            clock,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingFailsAndDoesNotPersist()
    {
        files.ThumbnailExists(false);
        var request = new CreateAnnouncementRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new CreateAnnouncementCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.AnnouncementThumbnailNotFound);
        await announcements
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Announcement>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestPersistsTrimmedAnnouncementAndInvalidatesCache()
    {
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var request = new CreateAnnouncementRequest(
            "  Title  ",
            "  Subtitle  ",
            "{\"x\":1}",
            thumbnailId
        );

        var result = await sut.HandleAsync(
            new CreateAnnouncementCommand(request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Title");
        result.Value.Subtitle.Should().Be("Subtitle");
        result.Value.Description.Should().Be("{\"x\":1}");
        result.Value.ThumbnailId.Should().Be(thumbnailId);
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        await announcements
            .Received(1)
            .AddAsync(
                Arg.Is<Announcement>(a =>
                    a != null
                    && a.Title == "Title"
                    && a.Subtitle == "Subtitle"
                    && a.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Announcements)
                )
            );
    }
}
