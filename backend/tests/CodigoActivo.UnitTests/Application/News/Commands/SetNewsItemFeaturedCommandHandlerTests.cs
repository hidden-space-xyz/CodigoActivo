using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.News;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.News.NewsTestData;

namespace CodigoActivo.UnitTests.Application.News.Commands;

public sealed class SetNewsItemFeaturedCommandHandlerTests
{
    private readonly INewsItemRepository news = Substitute.For<INewsItemRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>().RunsTransactions();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly SetNewsItemFeaturedCommandHandler sut;

    public SetNewsItemFeaturedCommandHandlerTests()
    {
        sut = new SetNewsItemFeaturedCommandHandler(news, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncIdMissingReturnsNotFound()
    {
        news.Finds(null);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.NewsItemNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncMarkedFeaturesTheRequestedNewsItem()
    {
        var chosen = NewNewsItem();
        news.Finds(chosen);
        news.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        chosen.Featured.Should().BeTrue();
        await news.Received(1).GetByIdAsync(chosen.Id, Arg.Any<CancellationToken>());
        await uow.Received(1)
            .ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncOtherItemFeaturedUnfeaturesItWithoutTouchingAuditFields()
    {
        var chosen = NewNewsItem();
        var previous = NewNewsItem(featured: true);
        news.Finds(chosen);
        news.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([previous]);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        chosen.Featured.Should().BeTrue();
        previous.Featured.Should().BeFalse();
        chosen.UpdatedAt.Should().BeNull();
        previous.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncOtherItemFeaturedSavesItsUnfeaturingBeforeFeaturingTheChosenOne()
    {
        var chosen = NewNewsItem();
        var previous = NewNewsItem(featured: true);
        news.Finds(chosen);
        news.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([previous]);
        var saved = new List<(bool Previous, bool Chosen)>();
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => saved.Add((previous.Featured, chosen.Featured)));

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        saved.Should().Equal((false, false), (false, true));
    }

    [Fact]
    public async Task HandleAsyncAlreadyFeaturedKeepsItFeatured()
    {
        var chosen = NewNewsItem(featured: true);
        news.Finds(chosen);
        news.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([chosen]);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        chosen.Featured.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncMarkedInvalidatesNewsCache()
    {
        var chosen = NewNewsItem();
        news.Finds(chosen);
        news.ListFeaturedAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(chosen.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.News)
                )
            );
    }
}
