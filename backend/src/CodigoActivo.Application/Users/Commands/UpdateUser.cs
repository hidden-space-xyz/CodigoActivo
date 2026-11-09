using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.EmailAddresses;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to update the user.
/// </summary>
/// <param name="UserId">Identifier of the account updated.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Email">Email; required for an independent account, ignored for a dependent.</param>
/// <param name="Phone">Phone; required for an independent account, ignored for a dependent.</param>
/// <param name="BirthDate">
/// Date of birth; required for a dependent, which a changed one must keep a minor, and refused for
/// an independent account.
/// </param>
/// <param name="NationalId">
/// DNI or NIE; required for an independent account, ignored for a dependent.
/// </param>
/// <param name="NewsConsent">
/// Whether the holder agrees to receive news by email; ignored for a dependent.
/// </param>
/// <param name="Gender">Gender.</param>
/// <param name="ParentId">
/// Guardian of a dependent, repeating the one it has, or <see langword="null"/>; refused for an
/// independent account.
/// </param>
/// <param name="CurrentPassword">
/// Password of the signed-in user, required to replace the email or the phones.
/// </param>
/// <param name="SecondaryPhone">
/// Optional second phone of an independent account, different from the phone; ignored for a
/// dependent.
/// </param>
/// <param name="Code">
/// Current code of the signed-in user's second factor, emailed or read from the authenticator,
/// required to replace the email.
/// </param>
public sealed record UpdateUserCommand(
    UserId UserId,
    [property: Required, MaxLength(120)] string FirstName,
    [property: Required, MaxLength(120)] string LastName,
    [property: EmailAddress, MaxLength(256)] string? Email,
    [property: MaxLength(40)] string? Phone,
    [property: NotDefaultOrFutureDate] DateOnly? BirthDate,
    [property: MaxLength(12), SpanishNationalId] string? NationalId,
    bool NewsConsent,
    [property: EnumDataType(typeof(Gender))] Gender Gender,
    UserId? ParentId,
    [property: MaxLength(128)] string? CurrentPassword,
    [property: MaxLength(40)] string? SecondaryPhone,
    [property: MaxLength(16)] string? Code
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the user. Only the holder or the guardian of a dependent may
/// update a person; an administrator cannot change anyone else's personal data or consent.
/// Replacing the email or the phones needs the password of the signed-in user. Replacing the email
/// also needs a current code of the second factor, checked and counted with the account row locked
/// like a login, because whoever holds the email receives every login code and reset link
/// afterwards; only the holder ever replaces it, since a dependent has no email. A new email only
/// applies once its holder confirms it through the emailed link; the rest applies at once.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">
/// Policy that decides whose personal data the signed-in user manages.
/// </param>
/// <param name="passwordAttempts">Guard that verifies the password of the signed-in user.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="disposableEmails">Checker of disposable email domains.</param>
/// <param name="emailChangeLinks">Issuer of the link that confirms a new email.</param>
/// <param name="accountLock">Runs the email change with the account locked.</param>
/// <param name="secondFactor">Guard that verifies, counts and locks second-factor codes.</param>
public sealed class UpdateUserCommandHandler(
    IUserRepository users,
    ActingUserPolicy actingUser,
    PasswordAttemptGuard passwordAttempts,
    IClock clock,
    DisposableEmailChecker disposableEmails,
    EmailChangeLinkIssuer emailChangeLinks,
    AccountLock accountLock,
    SecondFactorGuard secondFactor
) : ICommandHandler<UpdateUserCommand, Result>
{
    /// <summary>
    /// Handles the request to update the user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> when the caller is neither
    /// the user nor their guardian, <see cref="ApplicationErrorCode.UserNotFound"/>, the domain
    /// error of the profile rule that refuses the change,
    /// <see cref="ApplicationErrorCode.DisposableEmailNotAllowed"/>,
    /// <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/> when replacing the email or
    /// the phones with a wrong, missing or locked password,
    /// <see cref="ApplicationErrorCode.TwoFactorCodeInvalid"/> when replacing the email with a
    /// wrong, expired, reused or missing code, <see cref="DomainErrorCode.TwoFactorLocked"/> while
    /// the second factor is locked, or the error of <see cref="EmailChangeLinkIssuer.IssueAsync"/>
    /// when the confirmation link cannot be queued.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateUserCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayManagePersonalDataAsync(command.UserId, ct);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var planned = user.PlanProfileChange(
            new PersonDetails(
                command.FirstName,
                command.LastName,
                command.Gender,
                command.Email,
                command.Phone,
                command.SecondaryPhone,
                command.NationalId,
                command.NewsConsent,
                command.BirthDate
            ),
            command.ParentId,
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
            return Error.Validation(ApplicationErrorCode.DisposableEmailNotAllowed);
        }

        if (
            change.ReplacesContact
            && !await passwordAttempts.VerifyActingUserAsync(command.CurrentPassword, ct)
        )
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        if (change.NewEmail is not null)
        {
            return await ChangeEmailAsync(user, change, command.Code, ct);
        }

        user.ApplyProfileChange(change, clock.UtcNow);
        return Result.Success();
    }

    private async Task<Result> ChangeEmailAsync(
        User user,
        ProfileChange change,
        string? code,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.Validation(ApplicationErrorCode.TwoFactorCodeInvalid);
        }

        return await accountLock.RunAsync(
            user,
            async attempt =>
            {
                var now = clock.UtcNow;
                var verified = await secondFactor.VerifyLockedAsync(user, code, now, attempt);
                return verified.IsFailure
                    ? verified
                    : await emailChangeLinks.IssueAsync(user, change, now, attempt);
            },
            Error.NotFound(ApplicationErrorCode.UserNotFound),
            ct
        );
    }
}
