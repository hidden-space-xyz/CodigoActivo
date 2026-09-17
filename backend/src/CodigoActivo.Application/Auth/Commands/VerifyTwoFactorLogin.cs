using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to complete a login with the second factor.
/// </summary>
/// <param name="UserId">Identifier of the user whose password was already accepted.</param>
/// <param name="Code">Code from the email or the authenticator application.</param>
public sealed record VerifyTwoFactorLoginCommand(Guid UserId, string Code)
    : ICommand<Result<UserResponse>>;

/// <summary>
/// Executes the second step of the login. Wrong codes are counted and lock the account's second
/// factor for a while once the limit is reached, so short codes cannot be brute forced.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="otpValidator">Validator of emailed codes.</param>
/// <param name="authenticatorCodes">Verifier of authenticator codes.</param>
/// <param name="options">Second-factor configuration.</param>
public sealed class VerifyTwoFactorLoginCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    OtpValidator otpValidator,
    AuthenticatorCodeVerifier authenticatorCodes,
    TwoFactorOptions options
) : ICommandHandler<VerifyTwoFactorLoginCommand, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to complete the login.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the signed-in user on success, or an application error on failure.</returns>
    public async Task<Result<UserResponse>> HandleAsync(
        VerifyTwoFactorLoginCommand command,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        var now = clock.UtcNow;
        if (user.IsTwoFactorLocked(now))
        {
            return Error.Forbidden(ErrorCode.TwoFactorLocked);
        }

        long? usedStep = null;
        var accepted = user.TwoFactorMethod switch
        {
            TwoFactorMethod.Authenticator => (
                usedStep = authenticatorCodes.Match(
                    user.AuthenticatorKey,
                    command.Code,
                    user.AuthenticatorLastUsedStep
                )
            ) is not null,
            _ => otpValidator.IsCodeValid(
                command.Code,
                user.LoginCodeHash,
                user.LoginCodeExpiresAt
            ),
        };

        if (!accepted)
        {
            user.RecordTwoFactorFailure(now, options.MaxFailedAttempts, options.LockoutDuration);
            await uow.SaveChangesAsync(ct);
            return Error.BadRequest(ErrorCode.TwoFactorCodeInvalid);
        }

        if (usedStep is { } step)
        {
            user.AuthenticatorLastUsedStep = step;
        }

        user.CompleteTwoFactorLogin(now);
        await uow.SaveChangesAsync(ct);

        var signedIn = await users.GetByIdWithDetailsAsync(user.Id, ct);
        return signedIn!.ToResponse();
    }
}
