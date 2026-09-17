using AwesomeAssertions;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

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
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly TestClock clock = new();
    private readonly TermsGate sut;

    public TermsGateTests()
    {
        sut = new TermsGate(activities, events, new FakeQueryExecutor(), clock);
    }

    private void HasActivity(Guid activityId, Guid eventId)
    {
        activities
            .Query()
            .Returns(
                new List<Activity>
                {
                    new()
                    {
                        Id = activityId,
                        Title = "Taller",
                        Description = "{}",
                        Location = "Sala",
                        EventId = eventId,
                    },
                }.AsQueryable()
            );
    }

    private void HasDocuments(Guid eventId, params (Guid TermsDocumentId, bool Required)[] documents)
    {
        events
            .QueryTermsDocuments()
            .Returns(
                documents
                    .Select(d => new EventTermsDocument
                    {
                        EventId = eventId,
                        TermsDocumentId = d.TermsDocumentId,
                        IsRequired = d.Required,
                        DisplayOrder = 0,
                    })
                    .ToList()
                    .AsQueryable()
            );
    }

    private void HasAcceptances(params EventTermsAcceptance[] acceptances)
    {
        events
            .ListTermsAcceptancesAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(acceptances.ToList());
    }

    [Fact]
    public async Task EnsureDecidedAsyncActivityMissingReturnsActivityNotFound()
    {
        activities.Query().Returns(new List<Activity>().AsQueryable());

        var result = await sut.EnsureDecidedAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
    }

    [Fact]
    public async Task EnsureDecidedAsyncEventWithoutDocumentsSucceedsWithoutPersisting()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(Guid.NewGuid(), true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
        // An event with no linked documents never needs to know the user's prior decisions.
        await events
            .DidNotReceiveWithAnyArgs()
            .ListTermsAcceptancesAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncDecisionForUnlinkedDocumentIsIgnored()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var linkedRequiredId = Guid.NewGuid();
        var unlinkedId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (linkedRequiredId, true));
        HasAcceptances(
            new EventTermsAcceptance
            {
                TermsDocumentId = linkedRequiredId,
                Accepted = true,
                DecidedAt = clock.UtcNow,
            }
        );

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(unlinkedId, true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncRequiredDocumentWithStoredRejectionAcceptingOverwritesRowInPlace()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, true));
        var stored = new EventTermsAcceptance
        {
            TermsDocumentId = termsDocumentId,
            Accepted = false,
            DecidedAt = clock.UtcNow.AddDays(-1),
        };
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue(
            "a rejection is revisable, so accepting afterwards must not leave the user permanently excluded"
        );
        stored.Accepted.Should().BeTrue();
        stored.DecidedAt.Should().Be(clock.UtcNow);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncOptionalDocumentWithStoredRejectionAcceptingOverwritesRowInPlace()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, false));
        var stored = new EventTermsAcceptance
        {
            TermsDocumentId = termsDocumentId,
            Accepted = false,
            DecidedAt = clock.UtcNow.AddDays(-1),
        };
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, true)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        stored.Accepted.Should().BeTrue(
            "a rejection is revisable regardless of whether the document is required or optional"
        );
        stored.DecidedAt.Should().Be(clock.UtcNow);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncOptionalDocumentWithStoredRejectionRejectingAgainLeavesDecidedAtUnchanged()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, false));
        var originalDecidedAt = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        var stored = new EventTermsAcceptance
        {
            TermsDocumentId = termsDocumentId,
            Accepted = false,
            DecidedAt = originalDecidedAt,
        };
        HasAcceptances(stored);
        // Different from originalDecidedAt on purpose: the test must fail if a repeated
        // rejection is (wrongly) re-stamped with the clock's current instant.
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue(
            "an optional document never blocks the signup, regardless of the decision"
        );
        stored.Accepted.Should().BeFalse();
        stored.DecidedAt.Should().Be(
            originalDecidedAt,
            "a repeated rejection is not a change, so the original decision instant must survive"
        );
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncDocumentWithStoredAcceptanceRejectingIsIgnoredKeepingAcceptanceIntact()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, true));
        var originalDecidedAt = clock.UtcNow.AddDays(-1);
        var stored = new EventTermsAcceptance
        {
            TermsDocumentId = termsDocumentId,
            Accepted = true,
            DecidedAt = originalDecidedAt,
        };
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        stored.Accepted.Should().BeTrue(
            "an acceptance is the proof of consent and must never be overwritten by a later rejection"
        );
        stored.DecidedAt.Should().Be(originalDecidedAt);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncRequiredDocumentWithStoredRejectionRejectingAgainLeavesRowUnchangedAndStillBlocks()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, true));
        var originalDecidedAt = clock.UtcNow.AddDays(-1);
        var stored = new EventTermsAcceptance
        {
            TermsDocumentId = termsDocumentId,
            Accepted = false,
            DecidedAt = originalDecidedAt,
        };
        HasAcceptances(stored);
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        stored.Accepted.Should().BeFalse();
        stored.DecidedAt.Should().Be(originalDecidedAt);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncRejectingRequiredDocumentFailsWithoutPersisting()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, true));
        HasAcceptances();

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncMissingDecisionForRequiredDocumentFailsWithoutPersisting()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, true));
        HasAcceptances();

        var result = await sut.EnsureDecidedAsync(
            activityId,
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.EventTermsAcceptanceRequired);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddTermsAcceptanceAsync(
                new EventTermsAcceptance(),
                TestContext.Current.CancellationToken
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncRejectingOptionalDocumentPersistsRejectionAndSucceeds()
    {
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var termsDocumentId = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (termsDocumentId, false));
        HasAcceptances();
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            userId,
            [new TermsDecisionRequest(termsDocumentId, false)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue(
            "an optional document never blocks the signup, regardless of the decision"
        );
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
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
        var activityId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requiredOne = Guid.NewGuid();
        var requiredTwo = Guid.NewGuid();
        HasActivity(activityId, eventId);
        HasDocuments(eventId, (requiredOne, true), (requiredTwo, true));
        HasAcceptances();
        clock.UtcNow = new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

        var result = await sut.EnsureDecidedAsync(
            activityId,
            userId,
            [
                new TermsDecisionRequest(requiredOne, true),
                new TermsDecisionRequest(requiredTwo, true),
            ],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.TermsDocumentId == requiredOne
                    && a.Accepted
                    && a.DecidedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await events
            .Received(1)
            .AddTermsAcceptanceAsync(
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
