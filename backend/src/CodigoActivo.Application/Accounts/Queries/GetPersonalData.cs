using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the request for a copy of the signed-in user's personal data.
/// </summary>
public sealed record GetPersonalDataQuery : IQuery<Result<PersonalDataResponse>>;

/// <summary>
/// Executes the query that copies the personal data kept about the signed-in user and the minors
/// in their care.
/// </summary>
/// <param name="reader">Reader that copies the personal data of an account.</param>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class GetPersonalDataQueryHandler(PersonalDataReader reader, ICurrentUser currentUser)
    : IQueryHandler<GetPersonalDataQuery, Result<PersonalDataResponse>>
{
    /// <summary>
    /// Handles the request for a copy of the signed-in user's personal data.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the copy, or <see cref="ApplicationErrorCode.UserNotFound"/>
    /// when the account no longer exists.
    /// </returns>
    public Task<Result<PersonalDataResponse>> HandleAsync(
        GetPersonalDataQuery query,
        CancellationToken ct = default
    )
    {
        return reader.ReadAsync(currentUser.RequiredId().Value, ct);
    }
}
