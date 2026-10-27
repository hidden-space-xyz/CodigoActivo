using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Security;

/// <summary>
/// Decides whether the signed-in user may act for a person: for themselves, for one of their
/// dependents, or for anyone when they are an administrator. Personal data, the news consent and
/// dependents are narrower: only the holder or their guardian manages them, administrators
/// included. The guardianship is read from the read side, so commands and queries can both check
/// it without loading the account.
/// </summary>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="readStore">Read side the guardianship is read from.</param>
/// <param name="executor">Executor of the read-side queries.</param>
public sealed class ActingUserPolicy(
    ICurrentUser currentUser,
    IReadStore readStore,
    IQueryExecutor executor
)
{
    /// <summary>
    /// Checks that the signed-in user may act for a person.
    /// </summary>
    /// <param name="userId">Identifier of the person acted for.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> otherwise.
    /// </returns>
    public Task<Result> EnsureMayActForAsync(UserId userId, CancellationToken ct)
    {
        return currentUser.IsAdmin
            ? Task.FromResult(Result.Success())
            : EnsureMayManagePersonalDataAsync(userId, ct);
    }

    /// <summary>
    /// Checks that the signed-in user is the person or their guardian, the only ones who may change
    /// the person's personal data, news consent or dependents; being an administrator grants nothing.
    /// </summary>
    /// <param name="userId">Identifier of the person whose data would change.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> otherwise.
    /// </returns>
    public async Task<Result> EnsureMayManagePersonalDataAsync(UserId userId, CancellationToken ct)
    {
        var actingUserId = currentUser.RequiredId();
        if (actingUserId == userId)
        {
            return Result.Success();
        }

        var personId = userId.Value;
        var guardianId = actingUserId.Value;
        return await executor.AnyAsync(
            readStore.Users.Where(user => user.Id == personId && user.ParentId == guardianId),
            ct
        )
            ? Result.Success()
            : Error.Forbidden(ApplicationErrorCode.ActingForAnotherUserForbidden);
    }
}
