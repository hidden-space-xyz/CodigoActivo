using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Legal.Queries;

/// <summary>
/// Carries the request for the version in force of a legal policy.
/// </summary>
/// <param name="Kind">Which policy to read.</param>
public sealed record GetLegalPolicyQuery(LegalPolicyKind Kind)
    : IQuery<Result<LegalPolicyResponse>>;

/// <summary>
/// Executes the query that returns the version in force of a legal policy.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetLegalPolicyQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetLegalPolicyQuery, Result<LegalPolicyResponse>>
{
    /// <summary>
    /// Handles the request for the version in force of a legal policy.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the policy, or an application error when none was published yet.</returns>
    public async Task<Result<LegalPolicyResponse>> HandleAsync(
        GetLegalPolicyQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var response = await executor.FirstOrDefaultAsync(
            readStore
                .LegalPolicies.Where(policy => policy.Kind == query.Kind)
                .Select(LegalPolicyProjections.InForce),
            ct
        );
        return response is null
            ? Error.NotFound(ApplicationErrorCode.LegalPolicyNotFound)
            : response;
    }
}
