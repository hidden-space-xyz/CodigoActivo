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
/// Carries the input required to designate the account for data protection.
/// </summary>
/// <param name="UserId">Identifier of the account designated from now on.</param>
/// <param name="CurrentPassword">Password of the signed-in initial administrator.</param>
public sealed record DesignatePrivacyOfficerCommand(
    UserId UserId,
    [property: MaxLength(128)] string? CurrentPassword
) : ICommand<Result>;

/// <summary>
/// Executes the command to designate the account for data protection, the only one that consults
/// the blocked data, which must be an administrator. Only the initial administrator designates it,
/// itself included, and the account
/// designated until now stops being designated in the same transaction, so exactly one account is
/// designated at any time. Both holders are told by email.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="uow">Unit of work that saves the release before the designation.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class DesignatePrivacyOfficerCommandHandler(
    IUserRepository users,
    ICurrentUser currentUser,
    PasswordAttemptGuard passwordAttempts,
    IUnitOfWork uow,
    IClock clock
) : ICommandHandler<DesignatePrivacyOfficerCommand, Result>
{
    /// <summary>
    /// Handles the request to designate the account for data protection.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, also when the account was already designated;
    /// <see cref="ApplicationErrorCode.SuperAdministratorOnly"/> when the caller is not the initial
    /// administrator, <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/> for a wrong,
    /// missing or locked password, <see cref="ApplicationErrorCode.UserNotFound"/>,
    /// <see cref="DomainErrorCode.UserDependentCannotHoldStaffRole"/>,
    /// <see cref="DomainErrorCode.UserPendingVerificationCannotHoldStaffRole"/>,
    /// <see cref="DomainErrorCode.UserStaffRoleRequiresMember"/> or
    /// <see cref="DomainErrorCode.UserPrivacyOfficerMustBeAdministrator"/> for an account that is
    /// not an administrator.
    /// </returns>
    public async Task<Result> HandleAsync(
        DesignatePrivacyOfficerCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (currentUser.RequiredId() != InitialAdministrator.Id)
        {
            return Error.Forbidden(ApplicationErrorCode.SuperAdministratorOnly);
        }

        if (!await passwordAttempts.VerifyActingUserAsync(command.CurrentPassword, ct))
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        if (user.IsPrivacyOfficer)
        {
            return Result.Success();
        }

        var eligible = user.EnsureMayBePrivacyOfficer();
        if (eligible.IsFailure)
        {
            return eligible;
        }

        var now = clock.UtcNow;
        await uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                (await users.GetPrivacyOfficerAsync(attempt))?.ReleasePrivacyOfficer(now);
                await uow.SaveChangesAsync(attempt);
                if (user.DesignateAsPrivacyOfficer(now).IsFailure)
                {
                    throw new InvalidOperationException(
                        "An account eligible for the data protection designation refused it."
                    );
                }

                await uow.SaveChangesAsync(attempt);
                return true;
            },
            ct
        );
        return Result.Success();
    }
}
