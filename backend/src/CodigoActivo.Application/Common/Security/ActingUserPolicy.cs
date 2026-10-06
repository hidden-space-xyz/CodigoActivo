using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Security;

/// <summary>
/// Decides whether the signed-in user may act for a person: for themselves, for one of their
/// dependents, or for anyone when they are an administrator.
/// </summary>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
public sealed class ActingUserPolicy(ICurrentUser currentUser, IUserRepository users)
{
    /// <summary>
    /// Checks that the signed-in user may act for a person.
    /// </summary>
    /// <param name="userId">Identifier of the person acted for.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or a forbidden error otherwise.</returns>
    public async Task<Result> EnsureMayActForAsync(UserId userId, CancellationToken ct)
    {
        var actingUserId = currentUser.RequiredId();
        if (currentUser.IsAdmin || actingUserId == userId)
        {
            return Result.Success();
        }

        var person = await users.GetByIdAsync(userId, ct);
        return person is not null && person.IsDependentOf(actingUserId)
            ? Result.Success()
            : Error.Forbidden(ApplicationErrorCode.ActingForAnotherUserForbidden);
    }
}
