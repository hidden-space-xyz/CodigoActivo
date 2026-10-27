using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.TermsDecisions.Queries;

/// <summary>
/// Carries the criteria used to retrieve the signature drawn to accept a terms document of an event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the person the decision is about.</param>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
public sealed record GetEventTermsSignatureQuery(
    EventId EventId,
    UserId UserId,
    TermsDocumentId TermsDocumentId
) : IQuery<Result<byte[]>>;

/// <summary>
/// Executes the query to retrieve the signature drawn to accept a terms document of an event.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventTermsSignatureQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<GetEventTermsSignatureQuery, Result<byte[]>>
{
    /// <summary>
    /// Handles the request to retrieve the signature of a terms decision.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the PNG image of the signature, or
    /// <see cref="ApplicationErrorCode.EventTermsSignatureNotFound"/> when there is no decision or
    /// it has none, as after a withdrawal.
    /// </returns>
    public async Task<Result<byte[]>> HandleAsync(
        GetEventTermsSignatureQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var eventId = query.EventId.Value;
        var userId = query.UserId.Value;
        var termsDocumentId = query.TermsDocumentId.Value;
        var signature = await executor.FirstOrDefaultAsync(
            readStore
                .EventTermsAcceptances.Where(a =>
                    a.EventId == eventId
                    && a.UserId == userId
                    && a.TermsDocumentId == termsDocumentId
                )
                .Select(a => a.Signature),
            ct
        );
        return signature is null
            ? Error.NotFound(ApplicationErrorCode.EventTermsSignatureNotFound)
            : signature;
    }
}
