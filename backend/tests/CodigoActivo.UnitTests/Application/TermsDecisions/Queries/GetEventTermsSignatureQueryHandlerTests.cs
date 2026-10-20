using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.TermsDecisions.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.TermsDecisions.Queries;

public sealed class GetEventTermsSignatureQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly Guid eventId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid termsDocumentId = Guid.NewGuid();
    private readonly GetEventTermsSignatureQueryHandler sut;

    public GetEventTermsSignatureQueryHandlerTests()
    {
        sut = new GetEventTermsSignatureQueryHandler(store, new FakeQueryExecutor());
    }

    private void HasDecision(Guid onEvent, Guid byUser, Guid aboutDocument, byte[]? signature)
    {
        store.EventTermsAcceptances.Add(
            new EventTermsAcceptanceRow
            {
                EventId = onEvent,
                UserId = byUser,
                TermsDocumentId = aboutDocument,
                Accepted = true,
                Signature = signature,
            }
        );
    }

    private Task<Result<byte[]>> SignatureAsync()
    {
        return sut.HandleAsync(
            new GetEventTermsSignatureQuery(
                EventId.From(eventId),
                UserId.From(userId),
                TermsDocumentId.From(termsDocumentId)
            ),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncSignedDecisionReturnsItsImage()
    {
        var png = SignatureImages.Png();
        HasDecision(eventId, Guid.NewGuid(), termsDocumentId, SignatureImages.Png(10, 10));
        HasDecision(eventId, userId, Guid.NewGuid(), SignatureImages.Png(20, 20));
        HasDecision(Guid.NewGuid(), userId, termsDocumentId, SignatureImages.Png(30, 30));
        HasDecision(eventId, userId, termsDocumentId, png);

        var result = await SignatureAsync();

        result.Value.Should().Equal(png);
    }

    [Fact]
    public async Task HandleAsyncDecisionWithoutSignatureReturnsNotFound()
    {
        HasDecision(eventId, userId, termsDocumentId, null);

        var result = await SignatureAsync();

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventTermsSignatureNotFound);
    }

    [Fact]
    public async Task HandleAsyncDecisionMissingReturnsNotFound()
    {
        var result = await SignatureAsync();

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.EventTermsSignatureNotFound);
    }
}
