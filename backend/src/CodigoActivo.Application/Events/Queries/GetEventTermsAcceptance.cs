using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Queries;

public sealed record GetEventTermsAcceptanceQuery(Guid EventId, Guid UserId)
    : IQuery<EventTermsAcceptanceResponse>;

public sealed class GetEventTermsAcceptanceQueryHandler(
    IEventRepository events,
    IQueryExecutor executor
) : IQueryHandler<GetEventTermsAcceptanceQuery, EventTermsAcceptanceResponse>
{
    public async Task<EventTermsAcceptanceResponse> HandleAsync(
        GetEventTermsAcceptanceQuery query,
        CancellationToken ct = default
    )
    {
        var termsDocumentId = await executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == query.EventId).Select(e => e.TermsDocumentId),
            ct
        );
        return new EventTermsAcceptanceResponse(
            termsDocumentId is { } id
                && await events.TermsAcceptanceExistsAsync(query.EventId, query.UserId, id, ct)
        );
    }
}
