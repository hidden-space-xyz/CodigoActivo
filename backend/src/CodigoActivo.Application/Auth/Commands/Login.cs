using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to login.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record LoginCommand(LoginRequest Request) : ICommand<Result<LoginChallenge>>;

/// <summary>
/// Executes the password step of the login. A correct password never opens a session by itself:
/// it opens a second-factor challenge that <see cref="VerifyTwoFactorLoginCommandHandler"/> closes.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="credentialTiming">The credential timing value.</param>
/// <param name="twoFactor">Second-factor configuration.</param>
/// <param name="loginCodes">Issuer of emailed login codes.</param>
public sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    CredentialTimingProtector credentialTiming,
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
        var identifier = command.Request.Identifier.Trim();
        var user = await users.GetByEmailOrPhoneAsync(identifier, ct);

        if (
            user is null
            || !credentialTiming.Verify(
                command.Request.Password,
                string.IsNullOrEmpty(user.PasswordHash) ? null : user.PasswordHash
            )
        )
        {
            return Error.Unauthorized(ErrorCode.InvalidCredentials);
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked)
        {
            return Error.Forbidden(ErrorCode.UserAccountBlocked);
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent)
        {
            return Error.Forbidden(ErrorCode.UserAccountIsDependent);
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending)
        {
            return Error.Forbidden(ErrorCode.UserAccountPendingVerification);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ErrorCode.TwoFactorLocked);
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

        await uow.SaveChangesAsync(ct);

        return new LoginChallenge(
            user.Id,
            user.TwoFactorMethod,
            user.TwoFactorMethod == TwoFactorMethod.Email ? user.Email.MaskEmail() : null
        );
    }
}
