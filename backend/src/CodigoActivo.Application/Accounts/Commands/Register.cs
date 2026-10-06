using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to register.
/// </summary>
/// <param name="FirstName">Given name of the adult.</param>
/// <param name="LastName">Family name of the adult.</param>
/// <param name="Email">Email of the adult.</param>
/// <param name="Phone">Phone of the adult.</param>
/// <param name="Password">Password of the adult.</param>
/// <param name="NationalId">DNI or NIE of the adult.</param>
/// <param name="Gender">Gender of the adult.</param>
/// <param name="PromotionalConsent">Whether the adult agrees to receive promotional content.</param>
/// <param name="Minors">Minors registered with the adult.</param>
/// <param name="SecondaryPhone">Optional second phone of the adult.</param>
public sealed record RegisterCommand(
    [property: Required, MaxLength(120), NotBlank] string FirstName,
    [property: Required, MaxLength(120), NotBlank] string LastName,
    [property: Required, EmailAddress, MaxLength(256)] string Email,
    [property: Required, MaxLength(40)] string Phone,
    [property: Required, MinLength(12), MaxLength(128), NotBlank] string Password,
    [property: Required, MaxLength(12), NotBlank, SpanishNationalId] string NationalId,
    [property: EnumDataType(typeof(Gender))] Gender Gender,
    bool PromotionalConsent,
    [property: MaxLength(Household.MaxDependents)] IReadOnlyList<MinorDraft> Minors,
    [property: MaxLength(40)] string? SecondaryPhone
) : ICommand<Result>;

/// <summary>
/// Executes the command to register.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="hasher">The hasher value.</param>
/// <param name="verification">The verification value.</param>
/// <param name="accountEmails">The account emails value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
/// <param name="disposableEmails">Checker that refuses addresses of disposable email providers.</param>
/// <param name="emailClaims">Committer that replaces an unverified account holding the email.</param>
public sealed class RegisterCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails,
    ILogger<RegisterCommandHandler> logger,
    DisposableEmailChecker disposableEmails,
    EmailClaims emailClaims
) : ICommandHandler<RegisterCommand, Result>
{
    /// <summary>
    /// Handles the request to register. <see cref="User.CreateIndependent"/> and
    /// <see cref="User.CreateDependent"/> decide which details the adult and each minor need, and a
    /// disposable email is refused. Every other outcome answers alike, so the response never tells
    /// whether the address has an account: a new address gets the verification link; an address
    /// whose account <see cref="User.OwnsEmail"/> gets a notice instead and nothing is created; an
    /// address held by an account nobody verified gets the link of a new account that replaces it.
    /// The password and the code are hashed before the address is looked up, so every outcome does
    /// the same expensive work.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(RegisterCommand command, CancellationToken ct = default)
    {
        var planned = PlanHousehold(command);
        if (planned.IsFailure)
        {
            return planned.Error!;
        }

        var (adult, minors) = planned.Value;
        var email = adult.Email!;
        if (await disposableEmails.IsDisposableAsync(email, ct))
        {
            return Error.Validation(ApplicationErrorCode.DisposableEmailNotAllowed);
        }

        var now = clock.UtcNow;
        var otpCode = AccountTokens.Create();
        adult.AssignPassword(hasher.Hash(command.Password));
        adult.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);

        var holder = await users.GetByEmailAsync(email, ct);
        if (holder is { OwnsEmail: true })
        {
            await TrySendEmailInUseNoticeAsync(holder, ct);
            return Result.Success();
        }

        await users.AddAsync(adult, ct);
        foreach (var child in minors)
        {
            await users.AddAsync(child, ct);
        }

        if (await emailClaims.TryCommitAsync(adult, holder, now, ct))
        {
            await TrySendVerificationEmailAsync(adult, otpCode, ct);
        }

        return Result.Success();
    }

    private Result<NewHousehold> PlanHousehold(RegisterCommand request)
    {
        var minorRequests = request.Minors;
        if (
            string.IsNullOrWhiteSpace(request.Password)
            || minorRequests.Count > Household.MaxDependents
        )
        {
            return Error.Validation(ApplicationErrorCode.RegistrationInvalid);
        }

        var now = clock.UtcNow;
        var created = User.CreateIndependent(
            new PersonDetails(
                request.FirstName,
                request.LastName,
                request.Gender,
                request.Email,
                request.Phone,
                request.SecondaryPhone,
                request.NationalId,
                request.PromotionalConsent
            ),
            now
        );
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var adult = created.Value;
        var minors = new List<User>(minorRequests.Count);
        var children = minorRequests.Select(minor =>
            User.CreateDependent(adult, minor.ToDetails(), clock.Today, now)
        );
        foreach (var child in children)
        {
            if (child.IsFailure)
            {
                return child.Error!;
            }

            minors.Add(child.Value);
        }

        return new NewHousehold(adult, minors);
    }

    private async Task TrySendEmailInUseNoticeAsync(User holder, CancellationToken ct)
    {
        try
        {
            await accountEmails.SendEmailInUseNoticeAsync(holder, ct);
        }
        catch (EmailRateLimitedException)
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.AccountVerification, ex);
        }
    }

    private async Task TrySendVerificationEmailAsync(
        User user,
        string otpCode,
        CancellationToken ct
    )
    {
        try
        {
            await accountEmails.SendVerificationEmailAsync(user, otpCode, ct);
        }
        catch (EmailRateLimitedException)
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.AccountVerification, ex);

            user.ForgetOtpDelivery();
            await uow.SaveChangesAsync(ct);
        }
    }

    private sealed record NewHousehold(User Adult, IReadOnlyList<User> Minors);
}
