using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.BlockedData.Queries;

/// <summary>
/// Carries the request for one consultation of the blocked data.
/// </summary>
/// <param name="ConsultationId">Identifier of the consultation.</param>
public sealed record GetBlockedDataConsultationQuery(BlockedDataConsultationId ConsultationId)
    : IQuery<Result<BlockedDataConsultationResponse>>;

/// <summary>
/// Executes the query for one consultation of the blocked data, without any of its data.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetBlockedDataConsultationQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<GetBlockedDataConsultationQuery, Result<BlockedDataConsultationResponse>>
{
    /// <summary>
    /// Handles the request for one consultation of the blocked data.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the consultation, or the error.</returns>
    public async Task<Result<BlockedDataConsultationResponse>> HandleAsync(
        GetBlockedDataConsultationQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = query.ConsultationId.Value;
        var consultation = await executor.FirstOrDefaultAsync(
            BlockedDataProjections.Consultations(
                readStore.BlockedDataConsultations.Where(c => c.Id == id),
                readStore.Users
            ),
            ct
        );
        return consultation is null
            ? Error.NotFound(DomainErrorCode.BlockedDataConsultationNotFound)
            : consultation;
    }
}
