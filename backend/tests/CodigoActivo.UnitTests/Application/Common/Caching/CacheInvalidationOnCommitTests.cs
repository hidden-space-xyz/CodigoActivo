using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.Architecture;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Caching;

public sealed class CacheInvalidationOnCommitTests
{
    private static readonly Type[] EventsThatChangeNoCachedData =
    [
        typeof(AuthenticatorDisabled),
        typeof(AuthenticatorEnabled),
        typeof(ContactDetailsReplaced),
        typeof(PasswordChanged),
        typeof(PasswordLocked),
        typeof(PasswordReset),
        typeof(TermsConsentWithdrawn),
        typeof(TermsDocumentDeleted),
        typeof(TwoFactorLocked),
        typeof(TwoFactorReset),
    ];

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

    [Fact]
    public void DomainEventsAreMappedOrListedAsChangingNoCachedData()
    {
        var events = ProductionAssemblies
            .AllTypes.Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && typeof(IDomainEvent).IsAssignableFrom(type)
            )
            .ToList();
        var unclassified = events
            .Except(CacheTagsByEvent.MappedEvents)
            .Except(EventsThatChangeNoCachedData)
            .Select(type => type.Name)
            .ToList();

        events.Should().Contain(CacheTagsByEvent.MappedEvents.Concat(EventsThatChangeNoCachedData));
        CacheTagsByEvent.MappedEvents.Should().NotIntersectWith(EventsThatChangeNoCachedData);
        unclassified.Should().BeEmpty();
    }
}
