using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the input required to register.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record RegisterCommand(RegisterRequest Request) : ICommand<Result<Guid>>;

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
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="disposableEmails">Checker that refuses addresses of disposable email providers.</param>
public sealed class RegisterCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails,
    ILogger<RegisterCommandHandler> logger,
    ICacheInvalidator cacheInvalidator,
    DisposableEmailChecker disposableEmails
) : ICommandHandler<RegisterCommand, Result<Guid>>
{
    private const int MaxMinorRegistrations = 20;

    /// <summary>
    /// Handles the request to register. <see cref="User.CreateIndependent"/> and
    /// <see cref="User.CreateDependent"/> decide which details the adult and each minor need;
    /// this handler adds the checks that need I/O: a disposable or already registered email.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the new adult account, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
        RegisterCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;
        var minorRequests = request.Minors ?? [];
        if (
            string.IsNullOrWhiteSpace(request.Password)
            || minorRequests.Count > MaxMinorRegistrations
        )
        {
            return Error.Validation(ErrorCode.RequestValidationFailed);
        }

        var today = clock.Today;
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
        foreach (var minor in minorRequests)
        {
            var child = User.CreateDependent(
                adult,
                new PersonDetails(
                    minor.FirstName,
                    minor.LastName,
                    minor.Gender,
                    BirthDate: minor.BirthDate
                ),
                today,
                now
            );
            if (child.IsFailure)
            {
                return child.Error!;
            }

            minors.Add(child.Value);
        }

        var email = adult.Email!;
        if (await disposableEmails.IsDisposableAsync(email, ct))
        {
            return Error.Validation(ErrorCode.DisposableEmailNotAllowed);
        }

        if (await users.EmailExistsAsync(email, ct: ct))
        {
            return Error.Conflict(ErrorCode.UserEmailAlreadyInUse);
        }

        adult.AssignPassword(hasher.Hash(request.Password));
        var otpCode = AccountTokens.Create();
        adult.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);

        await users.AddAsync(adult, ct);
        foreach (var child in minors)
        {
            await users.AddAsync(child, ct);
        }

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        await TrySendVerificationEmailAsync(adult, otpCode, ct);

        return adult.Id;
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
}
