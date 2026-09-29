using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the criteria used to retrieve a newly registered account and the minors it registered.
/// </summary>
/// <param name="AdultId">Identifier of the registered adult account.</param>
public sealed record GetRegistrationQuery(Guid AdultId) : IQuery<Result<RegisterResponse>>;

/// <summary>
/// Executes the query to retrieve a newly registered account and the minors it registered.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetRegistrationQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetRegistrationQuery, Result<RegisterResponse>>
{
    /// <summary>
    /// Handles the request to retrieve a newly registered account and its minors.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the registration, or an application error when the account does not exist.</returns>
    public async Task<Result<RegisterResponse>> HandleAsync(
        GetRegistrationQuery query,
        CancellationToken ct = default
    )
    {
        var adult = await executor.FirstOrDefaultAsync(
            readStore.Users.Where(u => u.Id == query.AdultId).Select(UserProjections.User),
            ct
        );
        if (adult is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        var children = await executor.ToListAsync(
            readStore
                .Users.Where(u => u.ParentId == query.AdultId)
                .OrderBy(u => u.FirstName)
                .Select(UserProjections.User),
            ct
        );
        return new RegisterResponse(adult, children);
    }
}
