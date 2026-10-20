using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.Application.Activities;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.TermsDecisions;

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
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IEventTermsAcceptanceRepository termsAcceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private static readonly UserId RecorderId = UserId.From(Guid.NewGuid());

    private readonly TestClock clock = new();
    private readonly TermsGate sut;

    public TermsGateTests()
    {
        sut = new TermsGate(events, termsDocuments, termsAcceptances, clock);
    }

    private Guid HasDocuments(params (Guid TermsDocumentId, bool Required)[] documents)
    {
        var ev = Event.Create(
            new EventContent("Feria", "s", RichText.From("{}"), StoredFileId.From(Guid.NewGuid())),
            EventSchedule
                .Create(
                    new DateOnly(2026, 7, 1),
                    new DateOnly(2026, 7, 31),
                    null,
                    OpenStart,
                    OpenEnd
                )
                .Value,
            EventCategorySelection.Create([EventCategoryTypeId.From(Guid.NewGuid())]).Value,
            EventTermsLinks
                .Create(
                    [
                        .. documents.Select(d => new EventTermsLink(
                            TermsDocumentId.From(d.TermsDocumentId),
                            d.Required
                        )),
                    ],
                    []
                )
                .Value,
            UserId.From(Guid.NewGuid()),
            clock.UtcNow
        );
        events.GetByIdAsync(ev.Id, Arg.Any<CancellationToken>()).Returns(ev);
        return ev.Id.Value;
    }

    private void HasAcceptances(params EventTermsAcceptance[] acceptances)
    {
        termsAcceptances
            .ListAsync(Arg.Any<EventId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(acceptances);
    }

    private static IReadOnlyList<PersonTermsDecisions> Signup(
        Guid userId,
        IReadOnlyList<TermsDecision>? decisions
    )
    {
        return
        [
            new PersonTermsDecisions(
                Persisted.As<User>(new { Id = userId, Status = UserStatus.Active }),
                decisions
            ),
        ];
    }

    private void HoldConsents(params Guid[] termsDocumentIds)
    {
        termsDocuments
            .ListConsentsAsync(
                Arg.Any<IReadOnlyCollection<TermsDocumentId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([.. termsDocumentIds.Select(TermsDocumentId.From)]);
    }

    [Fact]
    public async Task EnsureDecidedAsyncEventMissingSucceedsWithoutRecording()
    {
        events.Finds(null);

        var result = await sut.EnsureDecidedAsync(
            EventId.From(Guid.NewGuid()),
            Signup(Guid.NewGuid(), [new TermsDecision(TermsDocumentId.New(), true)]),
            RecorderId,
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
            EventId.From(eventId),
            Signup(Guid.NewGuid(), [new TermsDecision(TermsDocumentId.New(), true)]),
            RecorderId,
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
            EventId.From(eventId),
            Signup(Guid.NewGuid(), [new TermsDecision(TermsDocumentId.From(unlinkedId), true)]),
            RecorderId,
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
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), true)]
            ),
            RecorderId,
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
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), true)]
            ),
            RecorderId,
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
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), false)]
            ),
            RecorderId,
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
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), false)]
            ),
            RecorderId,
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
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), false)]
            ),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventTermsAcceptanceRequired);
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
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), false)]
            ),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventTermsAcceptanceRequired);
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
            EventId.From(eventId),
            Signup(Guid.NewGuid(), null),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventTermsAcceptanceRequired);
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
            EventId.From(eventId),
            Signup(userId, [new TermsDecision(TermsDocumentId.From(termsDocumentId), false)]),
            RecorderId,
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
                    && a.EventId == EventId.From(eventId)
                    && a.UserId == UserId.From(userId)
                    && a.TermsDocumentId == TermsDocumentId.From(termsDocumentId)
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
            EventId.From(eventId),
            Signup(
                userId,
                [
                    new TermsDecision(TermsDocumentId.From(requiredOne), true),
                    new TermsDecision(TermsDocumentId.From(requiredTwo), true),
                ]
            ),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null
                    && a.TermsDocumentId == TermsDocumentId.From(requiredOne)
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
                    && a.TermsDocumentId == TermsDocumentId.From(requiredTwo)
                    && a.Accepted
                    && a.DecidedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
    }

    private void AsksForSignature(params Guid[] termsDocumentIds)
    {
        termsDocuments
            .ListRequiringSignatureAsync(
                Arg.Any<IReadOnlyCollection<TermsDocumentId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([.. termsDocumentIds.Select(TermsDocumentId.From)]);
    }

    [Fact]
    public async Task EnsureDecidedAsyncAcceptingDocumentThatAsksForSignatureRecordsTheSignature()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, true));
        HasAcceptances();
        AsksForSignature(termsDocumentId);
        var png = SignatureImages.Png();

        var result = await sut.EnsureDecidedAsync(
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), true, png)]
            ),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await termsAcceptances
            .Received(1)
            .AddAsync(
                Arg.Is<EventTermsAcceptance>(a =>
                    a != null && a.Accepted && a.Signature!.ToArray().SequenceEqual(png)
                ),
                Arg.Any<CancellationToken>()
            );
        await termsDocuments
            .Received(1)
            .ListRequiringSignatureAsync(
                Arg.Is<IReadOnlyCollection<TermsDocumentId>>(ids =>
                    ids.SequenceEqual(new[] { TermsDocumentId.From(termsDocumentId) })
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task EnsureDecidedAsyncAcceptingDocumentThatAsksForSignatureWithoutOneFailsWithoutPersisting()
    {
        var termsDocumentId = Guid.NewGuid();
        var eventId = HasDocuments((termsDocumentId, true));
        HasAcceptances();
        AsksForSignature(termsDocumentId);

        var result = await sut.EnsureDecidedAsync(
            EventId.From(eventId),
            Signup(
                Guid.NewGuid(),
                [new TermsDecision(TermsDocumentId.From(termsDocumentId), true)]
            ),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.EventTermsSignatureRequired);
        await termsAcceptances
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<EventTermsAcceptance>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncWithoutDecisionsDoesNotLookUpSignatures()
    {
        var eventId = HasDocuments((Guid.NewGuid(), false));
        HasAcceptances();

        var result = await sut.EnsureDecidedAsync(
            EventId.From(eventId),
            Signup(Guid.NewGuid(), []),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await termsDocuments
            .DidNotReceiveWithAnyArgs()
            .ListRequiringSignatureAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureDecidedAsyncConsentLinkedAsRequiredNeverBlocks()
    {
        var consentId = Guid.NewGuid();
        var eventId = HasDocuments((consentId, true));
        HasAcceptances();
        HoldConsents(consentId);

        var result = await sut.EnsureDecidedAsync(
            EventId.From(eventId),
            Signup(Guid.NewGuid(), null),
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureDecidedAsyncChecksEveryPersonAndRecordsWhoDecidedForEach()
    {
        var conditionsId = Guid.NewGuid();
        var consentId = Guid.NewGuid();
        var eventId = HasDocuments((conditionsId, true), (consentId, false));
        HoldConsents(consentId);
        var guardianId = Guid.NewGuid();
        var guardian = Persisted.As<User>(new { Id = guardianId, Status = UserStatus.Active });
        var teenager = Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                Status = UserStatus.Dependent,
                ParentId = guardianId,
                BirthDate = clock.Today.AddYears(-15),
            }
        );
        var child = Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                Status = UserStatus.Dependent,
                ParentId = guardianId,
                BirthDate = clock.Today.AddYears(-9),
            }
        );
        termsAcceptances
            .ListAsync(Arg.Any<EventId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var recorded = new List<EventTermsAcceptance>();
        await termsAcceptances.AddAsync(
            Arg.Do<EventTermsAcceptance>(recorded.Add),
            Arg.Any<CancellationToken>()
        );
        IReadOnlyList<TermsDecision> both =
        [
            new TermsDecision(TermsDocumentId.From(conditionsId), true),
            new TermsDecision(TermsDocumentId.From(consentId), true),
        ];

        var result = await sut.EnsureDecidedAsync(
            EventId.From(eventId),
            [
                new PersonTermsDecisions(guardian, both),
                new PersonTermsDecisions(teenager, both),
                new PersonTermsDecisions(child, both),
            ],
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        recorded
            .Select(acceptance =>
                (acceptance.UserId, acceptance.TermsDocumentId.Value, acceptance.DecidedById)
            )
            .Should()
            .BeEquivalentTo([
                (guardian.Id, conditionsId, guardian.Id),
                (guardian.Id, consentId, guardian.Id),
                (teenager.Id, conditionsId, guardian.Id),
                (teenager.Id, consentId, teenager.Id),
                (child.Id, conditionsId, guardian.Id),
                (child.Id, consentId, guardian.Id),
            ]);
    }

    [Fact]
    public async Task EnsureDecidedAsyncOnePersonMissingARequiredDocumentBlocksTheSignup()
    {
        var conditionsId = Guid.NewGuid();
        var eventId = HasDocuments((conditionsId, true));
        HasAcceptances();

        var result = await sut.EnsureDecidedAsync(
            EventId.From(eventId),
            [
                .. Signup(
                    Guid.NewGuid(),
                    [new TermsDecision(TermsDocumentId.From(conditionsId), true)]
                ),
                .. Signup(Guid.NewGuid(), null),
            ],
            RecorderId,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EventTermsAcceptanceRequired);
    }
}
