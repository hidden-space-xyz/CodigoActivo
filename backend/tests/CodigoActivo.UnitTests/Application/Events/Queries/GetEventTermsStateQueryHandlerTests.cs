using AwesomeAssertions;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class GetEventTermsStateQueryHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly GetEventTermsStateQueryHandler sut;

    public GetEventTermsStateQueryHandlerTests()
    {
        sut = new GetEventTermsStateQueryHandler(events, new FakeQueryExecutor());
    }

    private static EventTermsDocument NewDocument(
        Guid eventId,
        Guid termsDocumentId,
        bool required,
        int displayOrder,
        string name = "Documento"
    )
    {
        return new EventTermsDocument
        {
            EventId = eventId,
            TermsDocumentId = termsDocumentId,
            IsRequired = required,
            DisplayOrder = displayOrder,
            TermsDocument = new TermsDocument
            {
                Id = termsDocumentId,
                Name = name,
                Description = "{}",
            },
        };
    }

    private void HasDocuments(params EventTermsDocument[] documents)
    {
        events.QueryTermsDocuments().Returns(documents.AsQueryable());
    }

    private void HasAcceptances(params EventTermsAcceptance[] acceptances)
    {
        events.QueryTermsAcceptances().Returns(acceptances.AsQueryable());
    }

    [Fact]
    public async Task HandleAsyncEventWithoutDocumentsReturnsEmptyListAndSignupOpen()
    {
        var eventId = Guid.NewGuid();
        HasDocuments();

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(eventId, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Documents.Should().BeEmpty();
        result.SignupBlocked.Should().BeFalse();
        events.DidNotReceiveWithAnyArgs().QueryTermsAcceptances();
    }

    [Fact]
    public async Task HandleAsyncRequiredAndOptionalUndecidedBlocksSignupForMissingRequired()
    {
        var eventId = Guid.NewGuid();
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();
        HasDocuments(
            NewDocument(eventId, requiredId, required: true, displayOrder: 0, name: "Reglamento"),
            NewDocument(eventId, optionalId, required: false, displayOrder: 1, name: "Boletín")
        );
        HasAcceptances();

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(eventId, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Documents.Should().HaveCount(2);
        result.Documents.Should().OnlyContain(d => d.Accepted == null && d.DecidedAt == null);
        result.SignupBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncOnlyOptionalUndecidedDoesNotBlockSignup()
    {
        var eventId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();
        HasDocuments(NewDocument(eventId, optionalId, required: false, displayOrder: 0));
        HasAcceptances();

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(eventId, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Documents.Should().ContainSingle(d => d.Accepted == null);
        result.SignupBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncRequiredAcceptedWithOptionalUndecidedUnblocksSignup()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();
        var decidedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        HasDocuments(
            NewDocument(eventId, requiredId, required: true, displayOrder: 0),
            NewDocument(eventId, optionalId, required: false, displayOrder: 1)
        );
        HasAcceptances(
            new EventTermsAcceptance
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = requiredId,
                Accepted = true,
                DecidedAt = decidedAt,
            }
        );

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(eventId, userId),
            TestContext.Current.CancellationToken
        );

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
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requiredId = Guid.NewGuid();
        var optionalId = Guid.NewGuid();
        var decidedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        HasDocuments(
            NewDocument(eventId, requiredId, required: true, displayOrder: 0),
            NewDocument(eventId, optionalId, required: false, displayOrder: 1)
        );
        HasAcceptances(
            new EventTermsAcceptance
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = requiredId,
                Accepted = true,
                DecidedAt = decidedAt,
            },
            new EventTermsAcceptance
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = optionalId,
                Accepted = false,
                DecidedAt = decidedAt,
            }
        );

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(eventId, userId),
            TestContext.Current.CancellationToken
        );

        result
            .Documents.Should()
            .ContainSingle(d => d.TermsDocumentId == optionalId && d.Accepted == false);
        result.SignupBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncRequiredRejectedBlocksSignup()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requiredId = Guid.NewGuid();
        var decidedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        HasDocuments(NewDocument(eventId, requiredId, required: true, displayOrder: 0));
        HasAcceptances(
            new EventTermsAcceptance
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = requiredId,
                Accepted = false,
                DecidedAt = decidedAt,
            }
        );

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(eventId, userId),
            TestContext.Current.CancellationToken
        );

        result.Documents.Should().ContainSingle(d => d.Accepted == false);
        result.SignupBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncDocumentsOrderedByDisplayOrderRegardlessOfInputOrder()
    {
        var eventId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        HasDocuments(
            NewDocument(eventId, thirdId, required: false, displayOrder: 2, name: "Tercero"),
            NewDocument(eventId, firstId, required: false, displayOrder: 0, name: "Primero"),
            NewDocument(eventId, secondId, required: false, displayOrder: 1, name: "Segundo")
        );
        HasAcceptances();

        var result = await sut.HandleAsync(
            new GetEventTermsStateQuery(eventId, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Documents.Select(d => d.TermsDocumentId).Should().Equal(firstId, secondId, thirdId);
    }
}
