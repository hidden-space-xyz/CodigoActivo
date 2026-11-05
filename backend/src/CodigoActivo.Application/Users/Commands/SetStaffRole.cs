using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to change the staff role of a user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="StaffRole">Staff role of the user from now on.</param>
/// <param name="CurrentPassword">
/// Password of the signed-in initial administrator. Required to give a role; ignored when the
/// user is left without one.
/// </param>
public sealed record SetStaffRoleCommand(
    UserId UserId,
    [property: EnumDataType(typeof(StaffRole))] StaffRole StaffRole,
    [property: MaxLength(128)] string? CurrentPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to change the staff role of a user. Only the initial administrator changes
/// staff roles, never its own; only members who sign in by themselves and confirmed their email get
/// one, and the initial administrator and the account designated for data protection always stay
/// administrators. The holder is told by email when the role changes.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SetStaffRoleCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    PasswordAttemptGuard passwordAttempts,
    IClock clock
) : ICommandHandler<SetStaffRoleCommand, Result>
{
    /// <summary>
    /// Handles the request to change the staff role of a user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.SuperAdministratorOnly"/> when the caller is not the initial
    /// administrator, <see cref="ApplicationErrorCode.UserSelfStaffRoleChangeForbidden"/> for the
    /// caller's own account, <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/> when
    /// giving a role with a wrong, missing or locked password,
    /// <see cref="ApplicationErrorCode.UserNotFound"/>,
    /// <see cref="DomainErrorCode.UserDependentCannotHoldStaffRole"/>,
    /// <see cref="DomainErrorCode.UserPendingVerificationCannotHoldStaffRole"/>,
    /// <see cref="DomainErrorCode.UserStaffRoleRequiresMember"/>,
    /// <see cref="DomainErrorCode.UserCannotRemoveInitialAdmin"/> or
    /// <see cref="DomainErrorCode.UserPrivacyOfficerMustBeAdministrator"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        SetStaffRoleCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var actingUserId = currentUser.RequiredId();
        if (actingUserId != InitialAdministrator.Id)
        {
            return Error.Forbidden(ApplicationErrorCode.SuperAdministratorOnly);
        }

        if (command.UserId == actingUserId)
        {
            return Error.Forbidden(ApplicationErrorCode.UserSelfStaffRoleChangeForbidden);
        }

        if (
            command.StaffRole is not StaffRole.None
            && !await passwordAttempts.VerifyActingUserAsync(command.CurrentPassword, ct)
        )
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        return user.ChangeStaffRole(command.StaffRole, clock.UtcNow);
    }
}
