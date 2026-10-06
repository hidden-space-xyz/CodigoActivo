using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
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
/// <param name="BirthDate">Date of birth; required for a dependent, unset for an independent account.</param>
/// <param name="NationalId">DNI or NIE; required for an independent account, ignored for a dependent.</param>
/// <param name="PromotionalConsent">Whether the holder agrees to receive promotional content.</param>
/// <param name="Gender">Gender.</param>
/// <param name="ParentId">Guardian of a dependent, repeating the one it has.</param>
/// <param name="CurrentPassword">Password of the signed-in user, required to replace the email or the phones.</param>
/// <param name="SecondaryPhone">Optional second phone of an independent account.</param>
public sealed record UpdateUserCommand(
    UserId UserId,
    [property: Required, MaxLength(120), NotBlank] string FirstName,
    [property: Required, MaxLength(120), NotBlank] string LastName,
    [property: EmailAddress, MaxLength(256)] string? Email,
    [property: MaxLength(40)] string? Phone,
    [property: NotDefaultOrFutureDate] DateOnly? BirthDate,
    [property: MaxLength(12), SpanishNationalId] string? NationalId,
    bool PromotionalConsent,
    [property: EnumDataType(typeof(Gender))] Gender Gender,
    UserId? ParentId,
    [property: MaxLength(128)] string? CurrentPassword,
    [property: MaxLength(40)] string? SecondaryPhone
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the user. The signed-in user may update themselves or one of
/// their dependents; an administrator may update anyone. Replacing the email or the phones needs
/// the password of the signed-in user, and a new email of their own account only applies once
/// they confirm it.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="passwordAttempts">Guard that verifies the password of the signed-in user.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="disposableEmails">Checker of disposable email domains.</param>
/// <param name="emailChangeLinks">Issuer of the codes that confirm a new email.</param>
public sealed class UpdateUserCommandHandler(
    IUserRepository users,
    ActingUserPolicy actingUser,
    ICurrentUser currentUser,
    PasswordAttemptGuard passwordAttempts,
    IClock clock,
    DisposableEmailChecker disposableEmails,
    EmailChangeLinkIssuer emailChangeLinks
) : ICommandHandler<UpdateUserCommand, Result>
{
    /// <summary>
    /// Handles the request to update the user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(UpdateUserCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayActForAsync(command.UserId, ct);
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
                command.PromotionalConsent,
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

        var actingUserId = currentUser.RequiredId();
        if (
            change.ReplacesContact
            && !await VerifyActingPasswordAsync(actingUserId, command.CurrentPassword, ct)
        )
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var ownEmailChange = change.NewEmail is not null && command.UserId == actingUserId;
        if (
            !ownEmailChange
            && change.Email is { } email
            && await users.EmailExistsAsync(email, command.UserId, ct)
        )
        {
            return Error.Conflict(ApplicationErrorCode.UserEmailAlreadyInUse);
        }

        if (ownEmailChange)
        {
            return await emailChangeLinks.IssueAsync(user, change, clock.UtcNow, ct);
        }

        user.ApplyProfileChange(change, clock.UtcNow);
        return Result.Success();
    }

    private async Task<bool> VerifyActingPasswordAsync(
        UserId actingUserId,
        string? password,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var acting = await users.GetByIdAsync(actingUserId, ct);
        return await passwordAttempts.VerifyReauthenticationAsync(acting, password, ct);
    }
}
