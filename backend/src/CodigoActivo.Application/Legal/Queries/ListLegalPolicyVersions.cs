using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Legal.Queries;

/// <summary>
/// Carries the request for every version of a legal policy.
/// </summary>
/// <param name="Kind">Which policy to read.</param>
public sealed record ListLegalPolicyVersionsQuery(LegalPolicyKind Kind)
    : IQuery<Result<IReadOnlyList<LegalPolicyVersionResponse>>>;

/// <summary>
/// Executes the query that lists every version of a legal policy, newest first, starting with the
/// one in force.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListLegalPolicyVersionsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<ListLegalPolicyVersionsQuery, Result<IReadOnlyList<LegalPolicyVersionResponse>>>
{
    /// <summary>
    /// Handles the request for every version of a legal policy.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the versions, or
    /// <see cref="ApplicationErrorCode.LegalPolicyNotFound"/> when none was published yet.
    /// </returns>
    public async Task<Result<IReadOnlyList<LegalPolicyVersionResponse>>> HandleAsync(
        ListLegalPolicyVersionsQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var current = await executor.FirstOrDefaultAsync(
            readStore
                .LegalPolicies.Where(policy => policy.Kind == query.Kind)
                .Select(policy => new LegalPolicyVersionResponse
                {
                    Version = policy.Version,
                    PublishedAt = policy.PublishedAt,
                }),
            ct
        );
        if (current is null)
        {
            return Error.NotFound(ApplicationErrorCode.LegalPolicyNotFound);
        }

        var previous = await executor.ToListAsync(
            readStore
                .LegalPolicyVersions.Where(version => version.LegalPolicy.Kind == query.Kind)
                .OrderByDescending(version => version.Number)
                .Select(version => new LegalPolicyVersionResponse
                {
                    Version = version.Number,
                    PublishedAt = version.PublishedAt,
                }),
            ct
        );
        return Result.Success<IReadOnlyList<LegalPolicyVersionResponse>>([current, .. previous]);
    }
}
