using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Legal.Queries;

/// <summary>
/// Carries the request for one version of a legal policy.
/// </summary>
/// <param name="Kind">Which policy to read.</param>
/// <param name="Version">Number of the version, the one in force or one it replaced.</param>
public sealed record GetLegalPolicyVersionQuery(LegalPolicyKind Kind, int Version)
    : IQuery<Result<LegalPolicyResponse>>;

/// <summary>
/// Executes the query that returns one version of a legal policy with its content.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetLegalPolicyVersionQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetLegalPolicyVersionQuery, Result<LegalPolicyResponse>>
{
    /// <summary>
    /// Handles the request for one version of a legal policy.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the version, or an application error when the policy has none with that number.</returns>
    public async Task<Result<LegalPolicyResponse>> HandleAsync(
        GetLegalPolicyVersionQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var response =
            await executor.FirstOrDefaultAsync(
                readStore
                    .LegalPolicies.Where(policy =>
                        policy.Kind == query.Kind && policy.Version == query.Version
                    )
                    .Select(LegalPolicyProjections.InForce),
                ct
            )
            ?? await executor.FirstOrDefaultAsync(
                readStore
                    .LegalPolicyVersions.Where(version =>
                        version.LegalPolicy.Kind == query.Kind && version.Number == query.Version
                    )
                    .Select(LegalPolicyProjections.Replaced),
                ct
            );
        return response is null
            ? Error.NotFound(ApplicationErrorCode.LegalPolicyVersionNotFound)
            : response;
    }
}
