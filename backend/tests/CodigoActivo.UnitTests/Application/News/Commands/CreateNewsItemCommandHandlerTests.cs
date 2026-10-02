using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Application.News.Contracts;
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
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateNewsItemCommandHandler sut;

    public CreateNewsItemCommandHandlerTests()
    {
        sut = new CreateNewsItemCommandHandler(news, files, clock, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingFailsAndDoesNotPersist()
    {
        files.ThumbnailExists(false);
        var request = new CreateNewsItemRequest("Title", "Subtitle", "{}", Guid.NewGuid());

        var result = await sut.HandleAsync(
            new CreateNewsItemCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.NewsItemThumbnailNotFound);
        await news.DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<NewsItem>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestPersistsTrimmedNewsItemAndInvalidatesCache()
    {
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var added = new List<NewsItem>();
        await news.AddAsync(Arg.Do<NewsItem>(added.Add), Arg.Any<CancellationToken>());
        var request = new CreateNewsItemRequest(
            "  Title  ",
            "  Subtitle  ",
            "{\"x\":1}",
            thumbnailId
        );

        var result = await sut.HandleAsync(
            new CreateNewsItemCommand(request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Title.Should().Be("Title");
        created.Subtitle.Should().Be("Subtitle");
        created.Description.Should().Be("{\"x\":1}");
        created.ThumbnailId.Should().Be(thumbnailId);
        created.CreatedBy.Should().Be(caller);
        created.CreatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.News)
                )
            );
    }
}
