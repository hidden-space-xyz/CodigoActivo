using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.News.Commands;

public sealed class CreateNewsItemCommandHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly CreateNewsItemCommandHandler sut;

    public CreateNewsItemCommandHandlerTests()
    {
        sut = new CreateNewsItemCommandHandler(news, files, currentUser, clock);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingFailsAndDoesNotPersist()
    {
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(
            new CreateNewsItemCommand("Title", "Subtitle", "{}", StoredFileId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.NewsItemThumbnailNotFound);
        await news.DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<NewsItem>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidCommandStagesTrimmedNewsItemByTheCurrentUser()
    {
        files.ThumbnailExists(true);
        var thumbnailId = StoredFileId.New();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var added = new List<NewsItem>();
        await news.AddAsync(Arg.Do<NewsItem>(added.Add), Arg.Any<CancellationToken>());

        var result = await sut.HandleAsync(
            new CreateNewsItemCommand("  Title  ", "  Subtitle  ", "{\"x\":1}", thumbnailId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Title.Should().Be("Title");
        created.Subtitle.Should().Be("Subtitle");
        created.Description.Json.Should().Be("{\"x\":1}");
        created.ThumbnailId.Should().Be(thumbnailId);
        created.CreatedBy.Should().Be(currentUser.Id!.Value);
        created.CreatedAt.Should().Be(clock.UtcNow);
        created.PullDomainEvents().Should().Equal(new NewsItemCreated(created.Id));
    }
}
