using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Caching;

public sealed class CacheInvalidationOnCommitTests
{
    private sealed record UncachedEvent : IDomainEvent;

    private readonly ICacheInvalidator invalidator = Substitute.For<ICacheInvalidator>();
    private readonly CacheInvalidationOnCommit sut;

    public CacheInvalidationOnCommitTests()
    {
        sut = new CacheInvalidationOnCommit(invalidator);
    }

    [Fact]
    public async Task HandleAsyncMappedEventsInvalidatesTheirTagsOnce()
    {
        await sut.HandleAsync(
            [
                new PartnerCreated(PartnerId.New()),
                new PartnerDeleted(PartnerId.New(), StoredFileId.New()),
            ],
            TestContext.Current.CancellationToken
        );

        await invalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags.Count == 1 && tags.Contains(CacheTags.Partners)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncUnmappedEventsInvalidatesNothing()
    {
        await sut.HandleAsync([new UncachedEvent()], TestContext.Current.CancellationToken);

        await invalidator.DidNotReceiveWithAnyArgs().InvalidateAsync(Arg.Any<string[]>());
    }

    [Fact]
    public void MappedEventsAreAllDomainEvents()
    {
        CacheTagsByEvent.MappedEvents.Should().NotBeEmpty();
        CacheTagsByEvent
            .MappedEvents.Should()
            .OnlyContain(type => typeof(IDomainEvent).IsAssignableFrom(type));
    }
}
