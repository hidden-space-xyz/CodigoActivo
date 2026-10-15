using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetEventTermsStateQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid eventId = Guid.NewGuid();
    private readonly GetEventTermsStateQueryHandler sut;

    public GetEventTermsStateQueryHandlerTests()
    {
        store.Events.Add(new EventRow { Id = eventId });
        sut = new GetEventTermsStateQueryHandler(
            store,
            new FakeQueryExecutor(),
            new TestCurrentUser(UserId.From(userId))
        );
    }

    private static EventTermsDocumentRow NewDocument(
        Guid eventId,
        Guid termsDocumentId,
        bool required,
        int displayOrder,
        string name = "Documento",
        bool requiresSignature = false
    )
    {
        return new EventTermsDocumentRow
        {
            EventId = eventId,
            TermsDocumentId = termsDocumentId,
            IsRequired = required,
            DisplayOrder = displayOrder,
            TermsDocument = new TermsDocumentRow
            {
                Id = termsDocumentId,
                Name = name,
                Description = "{}",
                RequiresSignature = requiresSignature,
            },
        };
    }

    private void HasDocuments(params EventTermsDocumentRow[] documents)
    {
        store.EventTermsDocuments.AddRange(documents);
    }

    private void HasAcceptances(params EventTermsAcceptanceRow[] acceptances)
    {
        store.EventTermsAcceptances.AddRange(acceptances);
    }

    private async Task<EventTermsStateResponse> StateAsync()
    {
        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(EventId.From(eventId)),
            TestContext.Current.CancellationToken
        );
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        var unknownEventId = Guid.NewGuid();
        HasDocuments(NewDocument(unknownEventId, Guid.NewGuid(), required: true, displayOrder: 0));

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(EventId.From(unknownEventId)),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventNotFound);
        store.ReadsOf<EventTermsDocumentRow>().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncEventWithoutDocumentsReturnsEmptyListAndSignupOpen()
    {
        HasDocuments();

        var result = await StateAsync();

        result.Documents.Should().BeEmpty();
        result.SignupBlocked.Should().BeFalse();
        store.ReadsOf<EventTermsAcceptanceRow>().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncRequiredAndOptionalUndecidedBlocksSignupForMissingRequired()
    {
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();
        HasDocuments(
            NewDocument(eventId, requiredId, required: true, displayOrder: 0, name: "Reglamento"),
            NewDocument(eventId, optionalId, required: false, displayOrder: 1, name: "Boletín")
        );
        HasAcceptances();

        var result = await StateAsync();

        result.Documents.Should().HaveCount(2);
        result.Documents.Should().OnlyContain(d => d.Accepted == null && d.DecidedAt == null);
        result.SignupBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncOnlyOptionalUndecidedDoesNotBlockSignup()
    {
        var optionalId = Guid.NewGuid();
        HasDocuments(NewDocument(eventId, optionalId, required: false, displayOrder: 0));
        HasAcceptances();

        var result = await StateAsync();

        result.Documents.Should().ContainSingle(d => d.Accepted == null);
        result.SignupBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncRequiredAcceptedWithOptionalUndecidedUnblocksSignup()
    {
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();
        var decidedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        HasDocuments(
            NewDocument(eventId, requiredId, required: true, displayOrder: 0),
            NewDocument(eventId, optionalId, required: false, displayOrder: 1)
        );
        HasAcceptances(
            new EventTermsAcceptanceRow
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = requiredId,
                Accepted = true,
                DecidedAt = decidedAt,
            }
        );

        var result = await StateAsync();

        var required = result
            .Documents.Should()
            .ContainSingle(d => d.TermsDocumentId == requiredId)
            .Subject;
        required.Accepted.Should().BeTrue();
        required.DecidedAt.Should().Be(decidedAt);
        var optional = result
            .Documents.Should()
            .ContainSingle(d => d.TermsDocumentId == optionalId)
            .Subject;
        optional.Accepted.Should().BeNull();
        optional.DecidedAt.Should().BeNull();
        result
            .SignupBlocked.Should()
            .BeFalse(
                "every required document is accepted, regardless of undecided optional documents"
            );
    }

    [Fact]
    public async Task HandleAsyncOptionalRejectedDoesNotBlockSignup()
    {
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();
        var decidedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        HasDocuments(
            NewDocument(eventId, requiredId, required: true, displayOrder: 0),
            NewDocument(eventId, optionalId, required: false, displayOrder: 1)
        );
        HasAcceptances(
            new EventTermsAcceptanceRow
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = requiredId,
                Accepted = true,
                DecidedAt = decidedAt,
            },
            new EventTermsAcceptanceRow
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = optionalId,
                Accepted = false,
                DecidedAt = decidedAt,
            }
        );

        var result = await StateAsync();

        result
            .Documents.Should()
            .ContainSingle(d => d.TermsDocumentId == optionalId && d.Accepted == false);
        result.SignupBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncRequiredRejectedBlocksSignup()
    {
        var requiredId = Guid.NewGuid();
        var decidedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        HasDocuments(NewDocument(eventId, requiredId, required: true, displayOrder: 0));
        HasAcceptances(
            new EventTermsAcceptanceRow
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = requiredId,
                Accepted = false,
                DecidedAt = decidedAt,
            }
        );

        var result = await StateAsync();

        result.Documents.Should().ContainSingle(d => d.Accepted == false);
        result.SignupBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncDocumentsOrderedByDisplayOrderRegardlessOfInputOrder()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        HasDocuments(
            NewDocument(eventId, thirdId, required: false, displayOrder: 2, name: "Tercero"),
            NewDocument(eventId, firstId, required: false, displayOrder: 0, name: "Primero"),
            NewDocument(eventId, secondId, required: false, displayOrder: 1, name: "Segundo")
        );
        HasAcceptances();

        var result = await StateAsync();

        result.Documents.Select(d => d.TermsDocumentId).Should().Equal(firstId, secondId, thirdId);
    }

    [Fact]
    public async Task HandleAsyncDocumentThatAsksForSignatureTellsSo()
    {
        var signedId = Guid.NewGuid();
        var plainId = Guid.NewGuid();
        HasDocuments(
            NewDocument(
                eventId,
                signedId,
                required: true,
                displayOrder: 0,
                requiresSignature: true
            ),
            NewDocument(eventId, plainId, required: true, displayOrder: 1)
        );

        var result = await StateAsync();

        result
            .Documents.Select(d => (d.TermsDocumentId, d.RequiresSignature))
            .Should()
            .Equal((signedId, true), (plainId, false));
    }
}
