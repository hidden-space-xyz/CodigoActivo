using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
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
    private readonly SetNewsItemFeaturedCommandHandler sut;

    public SetNewsItemFeaturedCommandHandlerTests()
    {
        sut = new SetNewsItemFeaturedCommandHandler(news, uow);
    }

    [Fact]
    public async Task HandleAsyncIdMissingReturnsNotFound()
    {
        news.Finds(null);

        var result = await sut.HandleAsync(
            new SetNewsItemFeaturedCommand(NewsItemId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.NewsItemNotFound);
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
        chosen.PullDomainEvents().Should().Equal(new NewsItemFeaturedChanged(chosen.Id, true));
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
        previous.PullDomainEvents().Should().Equal(new NewsItemFeaturedChanged(previous.Id, false));
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
    public async Task HandleAsyncAlreadyFeaturedKeepsItFeaturedWithoutRaisingAChange()
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
        chosen.PullDomainEvents().Should().BeEmpty();
    }
}
