using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to update the user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActingUserId">Identifier of the acting user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record UpdateUserCommand(Guid UserId, Guid ActingUserId, UpdateUserRequest Request)
    : ICommand<Result>;

/// <summary>
/// Executes the command to update the user.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
/// <param name="disposableEmails">Checker that refuses addresses of disposable email providers.</param>
/// <param name="emailChangeLinks">Issuer of the links that confirm a new email.</param>
public sealed class UpdateUserCommandHandler(
    IUserRepository users,
    PasswordAttemptGuard passwordAttempts,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    AccountSecurityNotifier securityNotifier,
    DisposableEmailChecker disposableEmails,
    EmailChangeLinkIssuer emailChangeLinks
) : ICommandHandler<UpdateUserCommand, Result>
{
    /// <summary>
    /// Handles the request to update the user. <see cref="User.PlanProfileChange"/> decides which
    /// rules apply to the stored account; this handler adds the checks that need I/O before the
    /// change is applied. Only the email must be unique; the phone and the DNI or NIE are never
    /// checked against other accounts, so an update cannot reveal who uses them. Replacing the
    /// email, the phone or the secondary phone of the account first re-authenticates the acting
    /// caller, so a hijacked session alone cannot take the account over or redirect its contact
    /// details; the DNI or NIE needs no password. A new email is refused when it belongs to a
    /// disposable email provider, while an unchanged one is kept even if its domain was listed
    /// later. Holders who change their own email keep the stored one until they confirm the link
    /// emailed to the new address, are never told whether that address has an account, and change
    /// nothing when the mail cannot be sent; an administrator editing someone else's account
    /// replaces it at once, or is told the address is in use. Dependents are created only through
    /// <c>POST /api/users/{id}/children</c>, and they leave their guardian only when the guardian
    /// deletes them.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(UpdateUserCommand command, CancellationToken ct = default)
    {
        var request = command.Request;

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        var planned = user.PlanProfileChange(
            new PersonDetails(
                request.FirstName,
                request.LastName,
                request.Gender,
                request.Email,
                request.Phone,
                request.SecondaryPhone,
                request.NationalId,
                request.PromotionalConsent,
                request.BirthDate
            ),
            request.ParentId,
            clock.Today
        );
        if (planned.IsFailure)
        {
            return planned.Error!;
        }

        var change = planned.Value;
        if (
            change.NewEmail is { } newEmail
            && await disposableEmails.IsDisposableAsync(newEmail, ct)
        )
        {
            return Error.Validation(ErrorCode.DisposableEmailNotAllowed);
        }

        if (change.ReplacesContact && !await VerifyActingPasswordAsync(command, ct))
        {
            return Error.Validation(ErrorCode.UserCurrentPasswordIncorrect);
        }

        var ownEmailChange = change.NewEmail is not null && command.UserId == command.ActingUserId;
        if (
            !ownEmailChange
            && change.Email is { } email
            && await users.EmailExistsAsync(email, command.UserId, ct)
        )
        {
            return Error.Conflict(ErrorCode.UserEmailAlreadyInUse);
        }

        var previousEmail = user.Email;
        if (ownEmailChange)
        {
            var issued = await emailChangeLinks.IssueAsync(user, change, clock.UtcNow, ct);
            if (issued.IsFailure)
            {
                return issued.Error!;
            }
        }
        else
        {
            user.ApplyProfileChange(change, clock.UtcNow);
        }

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        var replacedEmail = ownEmailChange ? null : change.NewEmail;
        if ((replacedEmail is not null || change.ReplacesPhones) && previousEmail is not null)
        {
            await securityNotifier.NotifyIdentifiersChangedAsync(
                previousEmail,
                user.FirstName,
                replacedEmail,
                change.ReplacesPhones,
                ct
            );
        }

        return Result.Success();
    }

    private async Task<bool> VerifyActingPasswordAsync(
        UpdateUserCommand command,
        CancellationToken ct
    )
    {
        var password = command.Request.CurrentPassword;
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var actingUser = await users.GetByIdAsync(command.ActingUserId, ct);
        return await passwordAttempts.VerifyReauthenticationAsync(actingUser, password, ct);
    }
}
