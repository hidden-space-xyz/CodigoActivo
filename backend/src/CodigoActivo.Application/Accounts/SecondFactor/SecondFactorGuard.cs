using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.SecondFactor;

/// <summary>
/// Checks the codes of an account's active second factor: on login and on the routes that ask the
/// caller for a code before an irreversible change. The caller holds the account row locked and
/// reloaded, so parallel attempts are counted one after another. A locked second factor refuses
/// every code; a wrong code is counted and saved here, inside the caller's transaction and together
/// with any other staged change, because the calling handler then fails, and it locks the second
/// factor for a while once the limit is reached, so short codes cannot be brute forced. The lock
/// raises <see cref="TwoFactorLocked"/>, recorded in the security log once it is committed. An
/// accepted authenticator code records its time step on the account for the caller to save with
/// its change.
/// </summary>
/// <param name="uow">Unit of work used to commit the counted failure.</param>
/// <param name="otpValidator">Validator of emailed codes.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
public sealed class SecondFactorGuard(
    IUnitOfWork uow,
    OtpValidator otpValidator,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options
)
{
    /// <summary>
    /// Verifies a code of the account's second factor while its row is locked.
    /// </summary>
    /// <param name="user">Account, locked and reloaded by the caller.</param>
    /// <param name="code">Code from the email or the authenticator application.</param>
    /// <param name="now">Current timestamp.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is success, or the reason the code was refused.</returns>
    public async Task<Result> VerifyLockedAsync(
        User user,
        string code,
        DateTimeOffset now,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(DomainErrorCode.TwoFactorLocked);
        }

        long? usedStep = null;
        var accepted = user.TwoFactorMethod switch
        {
            TwoFactorMethod.Authenticator => (
                usedStep = authenticatorCodes.Match(user.AuthenticatorKey, code)
            )
                is { } matched
                && user.AcceptsAuthenticatorStep(matched),
            _ => otpValidator.IsCodeValid(code, user.UsableLoginCodeHash(now)),
        };

        if (!accepted)
        {
            user.RecordTwoFactorFailure(now, options.MaxFailedAttempts, options.LockoutDuration);
            await uow.SaveChangesAsync(ct);
            return Error.Validation(ApplicationErrorCode.TwoFactorCodeInvalid);
        }

        if (usedStep is { } step)
        {
            user.RecordAuthenticatorStep(step);
        }

        return Result.Success();
    }
}
