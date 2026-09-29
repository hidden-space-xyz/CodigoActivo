using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities;

/// <summary>
/// Exercises <see cref="TermsGate"/> directly, at the unit that owns the terms decision rules,
/// instead of only through the command handlers that call it. Some of these scenarios are also
/// covered end-to-end by <c>AssignActivityCommandHandlerTests</c>; they are repeated here so the
/// gate's own contract (idempotency, never persisting a rejected required document, never touching
/// <see cref="IUnitOfWork"/>) is verified independently of any single caller.
/// </summary>
public sealed class TermsGateTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IEventTermsAcceptanceRepository termsAcceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private readonly TestClock clock = new();
    private readonly TermsGate sut;

    public TermsGateTests()
    {
        sut = new TermsGate(events, termsAcceptances, clock);
    }

    private Guid HasDocuments(params (Guid TermsDocumentId, bool Required)[] documents)
    {
        var ev = Event.Create(
            new EventContent("Feria", "s", "{}", Guid.NewGuid()),
            EventSchedule
                .Create(
                    new DateOnly(2026, 7, 1),
                    new DateOnly(2026, 7, 31),
                    null,
                    OpenStart,
                    OpenEnd
                )
                .Value,
            EventCategorySelection.Create([Guid.NewGuid()]).Value,
            EventTermsLinks
                .Create([
                    .. documents.Select(d => new EventTermsLink(d.TermsDocumentId, d.Required)),
                ])
                .Value,
            Guid.NewGuid(),
            clock.UtcNow
        );
        events.GetByIdAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        return ev.Id;
    }

    private void HasAcceptances(params EventTermsAcceptance[] acceptances)
    {
        termsAcceptances
            .ListAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(acceptances);
    }

    [Fact]
    public async Task EnsureDecidedAsyncEventMissingSucceedsWithoutRecording()
    {
        events.Finds(null);

        var result = await sut.EnsureDecidedAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new TermsDecisionRequest(Guid.NewGuid(), true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncEventWithoutDocumentsSucceedsWithoutPersisting()
    {
        var eventId = HasDocuments();

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(Guid.NewGuid(), true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .ListAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncDecisionForUnlinkedDocumentIsIgnored()
    {
        var linkedRequiredId = Guid.NewGuid();
        var unlinkedId = Guid.NewGuid();
        var eventId = HasDocuments((linkedRequiredId, true));
        HasAcceptances(StoredDecision(linkedRequiredId, true, clock.UtcNow));

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(unlinkedId, true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncRequiredDocumentWithStoredRejectionAcceptingOverwritesRowInPlace()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, true));
        var stored = StoredDecision(termsDocumentId, false, clock.UtcNow.AddDays(-1));
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, true)],
            TestContext.Current.CancellationToken
        );

        result
            .IsSuccess.Should()
            .BeTrue(
                "a rejection is revisable, so accepting afterwards must not leave the user permanently excluded"
            );
        stored.Accepted.Should().BeTrue();
        stored.DecidedAt.Should().Be(clock.UtcNow);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncOptionalDocumentWithStoredRejectionAcceptingOverwritesRowInPlace()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, false));
        var stored = StoredDecision(termsDocumentId, false, clock.UtcNow.AddDays(-1));
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        stored
            .Accepted.Should()
            .BeTrue(
                "a rejection is revisable regardless of whether the document is required or optional"
            );
        stored.DecidedAt.Should().Be(clock.UtcNow);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncOptionalDocumentWithStoredRejectionRejectingAgainLeavesDecidedAtUnchanged()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, false));
        var originalDecidedAt = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var stored = StoredDecision(termsDocumentId, false, originalDecidedAt);
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result
            .IsSuccess.Should()
            .BeTrue("an optional document never blocks the signup, regardless of the decision");
        stored.Accepted.Should().BeFalse();
        stored
            .DecidedAt.Should()
            .Be(
                originalDecidedAt,
                "a repeated rejection is not a change, so the original decision instant must survive"
            );
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncDocumentWithStoredAcceptanceRejectingIsIgnoredKeepingAcceptanceIntact()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, true));
        var originalDecidedAt = clock.UtcNow.AddDays(-1);
        var stored = StoredDecision(termsDocumentId, true, originalDecidedAt);
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        stored
            .Accepted.Should()
            .BeTrue(
                "an acceptance is the proof of consent and must never be overwritten by a later rejection"
            );
        stored.DecidedAt.Should().Be(originalDecidedAt);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncRequiredDocumentWithStoredRejectionRejectingAgainLeavesRowUnchangedAndStillBlocks()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, true));
        var originalDecidedAt = clock.UtcNow.AddDays(-1);
        var stored = StoredDecision(termsDocumentId, false, originalDecidedAt);
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        stored.Accepted.Should().BeFalse();
        stored.DecidedAt.Should().Be(originalDecidedAt);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncRejectingRequiredDocumentFailsWithoutPersisting()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, true));
        HasAcceptances();

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncMissingDecisionForRequiredDocumentFailsWithoutPersisting()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, true));
        HasAcceptances();

        var result = await sut.EnsureDecidedAsync(
            eventId,
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncRejectingOptionalDocumentPersistsRejectionAndSucceeds()
    {
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, false));
        HasAcceptances();
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            eventId,
            userId,
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result
            .IsSuccess.Should()
            .BeTrue("an optional document never blocks the signup, regardless of the decision");
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.EventId == eventId
                    && a.UserId == userId
                    && a.TermsDocumentId == termsDocumentId
                    && !a.Accepted
                    && a.DecidedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncAllRequiredAcceptedSucceedsWithClockTimestamp()
    {
        var userId = Guid.NewGuid();
        var requiredOne = Guid.NewGuid();
        var requiredTwo = Guid.NewGuid();
        var eventId = HasDocuments((requiredOne, true), (requiredTwo, true));
        HasAcceptances();
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            eventId,
            userId,
            [
                new TermsDecisionRequest(requiredOne, true),
                new TermsDecisionRequest(requiredTwo, true),
            ],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.TermsDocumentId == requiredOne
                    && a.Accepted
                    && a.DecidedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.TermsDocumentId == requiredTwo
                    && a.Accepted
                    && a.DecidedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
