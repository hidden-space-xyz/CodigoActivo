using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the pair of accounts whose guardianship is checked.
/// </summary>
/// <param name="GuardianId">Identifier of the account acting as guardian.</param>
/// <param name="UserId">Identifier of the account that may be one of their dependents.</param>
public sealed record IsGuardianOfQuery(Guid GuardianId, Guid UserId) : IQuery<bool>;

/// <summary>
/// Tells whether an account is the guardian of another one, which lets the guardian act on the
/// dependent's behalf.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class IsGuardianOfQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<IsGuardianOfQuery, bool>
{
    /// <summary>
    /// Handles the request to check a guardianship.
    /// </summary>
    /// <param name="query">Query containing both accounts.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the first account is the guardian of the second.</returns>
    public Task<bool> HandleAsync(IsGuardianOfQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return executor.AnyAsync(
            readStore.Users.Where(user =>
                user.Id == query.UserId && user.ParentId == query.GuardianId
            ),
            ct
        );
    }
}
