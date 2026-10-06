using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Mapping;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to login.
/// </summary>
/// <param name="Identifier">Email of the account.</param>
/// <param name="Password">Password of the account.</param>
public sealed record LoginCommand(
    [property: Required, MaxLength(256), NotBlank] string Identifier,
    [property: Required, MaxLength(128), NotBlank] string Password
) : ICommand<Result<LoginChallenge>>;

/// <summary>
/// Executes the password step of the login. A correct password never opens a session by itself:
/// it opens a second-factor challenge that <see cref="VerifyTwoFactorLoginCommandHandler"/> closes.
/// The identifier is the account email, the only value that stays unique across accounts.
/// An identifier matching no account, and an account locked after repeated wrong passwords, still
/// pay the same Argon2 work and answer the same error, so neither the response nor its timing
/// discloses which identifiers exist or which accounts are locked. Every accepted password step
/// stores a fresh challenge identifier, so the challenge cookie of an earlier attempt stops working.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="twoFactor">Second-factor configuration.</param>
/// <param name="loginCodes">Issuer of emailed login codes.</param>
public sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    PasswordAttemptGuard passwordAttempts,
    TwoFactorOptions twoFactor,
    LoginCodeIssuer loginCodes
) : ICommandHandler<LoginCommand, Result<LoginChallenge>>
{
    /// <summary>
    /// Handles the request to login.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the pending challenge on success, or an application error on failure.</returns>
    public async Task<Result<LoginChallenge>> HandleAsync(
        LoginCommand command,
        CancellationToken ct = default
    )
    {
        var email = EmailAddress.Create(command.Identifier);
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, ct) : null;

        var accepted = await passwordAttempts.VerifyLoginPasswordAsync(user, command.Password, ct);

        if (user is null)
        {
            return Error.Unauthorized(ApplicationErrorCode.InvalidCredentials);
        }

        if (!accepted)
        {
            return Error.Unauthorized(ApplicationErrorCode.InvalidCredentials);
        }

        if (user.IsBlocked)
        {
            return Error.Forbidden(ApplicationErrorCode.UserAccountBlocked);
        }

        if (user.IsDependent)
        {
            return Error.Forbidden(ApplicationErrorCode.UserAccountIsDependent);
        }

        if (user.IsPendingVerification)
        {
            return Error.Forbidden(ApplicationErrorCode.UserAccountPendingVerification);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ApplicationErrorCode.TwoFactorLocked);
        }

        if (
            user.TwoFactorMethod == TwoFactorMethod.Email
            && !user.HasRecentLoginCode(now, twoFactor.ResendCooldown)
        )
        {
            var issued = await loginCodes.IssueAsync(user, now, ct);
            if (issued.IsFailure)
            {
                return issued.Error!;
            }
        }

        user.StartLoginChallenge(Guid.NewGuid());
        await uow.SaveChangesAsync(ct);

        return new LoginChallenge(
            user.Id.Value,
            user.TwoFactorMethod.ToContract(),
            user.TwoFactorMethod == TwoFactorMethod.Email ? user.Email.MaskEmail() : null
        );
    }
}
