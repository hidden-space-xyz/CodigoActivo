using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Diagnostics;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Auth.Commands;

/// <summary>
/// Carries the input required to register.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record RegisterCommand(RegisterRequest Request) : ICommand<Result<RegisterResponse>>;

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
public sealed class RegisterCommandHandler(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    AccountVerificationOptions verification,
    AccountEmails accountEmails,
    ILogger<RegisterCommandHandler> logger,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<RegisterCommand, Result<RegisterResponse>>
{
    private const int MaxMinorRegistrations = 20;

    /// <summary>
    /// Handles the request to register.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a register on success, or an application error on failure.</returns>
    public async Task<Result<RegisterResponse>> HandleAsync(
        RegisterCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var today = clock.Today;

        var email = request.Email.NormalizeEmailOrNull();
        var phone = request.Phone.NormalizeOrNull();
        var secondaryPhone = request.SecondaryPhone.NormalizeOrNull();
        var nationalId = request.NationalId.NormalizeNationalIdOrNull();
        if (email is null || phone is null || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.BadRequest(ErrorCode.RegisterContactInfoRequired);
        }

        if (nationalId is null)
        {
            return Error.BadRequest(ErrorCode.RequestValidationFailed);
        }

        if (string.Equals(secondaryPhone, phone, StringComparison.Ordinal))
        {
            return Error.BadRequest(ErrorCode.SecondaryPhoneSameAsPrimary);
        }

        if (await users.ExistsAsync(u => u.Email == email, ct))
        {
            return Error.Conflict(ErrorCode.RegisterEmailAlreadyInUse);
        }

        var minorRequests = request.Minors ?? [];
        if (minorRequests.Count > MaxMinorRegistrations)
        {
            return Error.BadRequest(ErrorCode.RequestValidationFailed);
        }

        if (minorRequests.Any(minor => !minor.BirthDate.IsMinor(today)))
        {
            return Error.BadRequest(ErrorCode.RegisterMinorBirthDateNotMinor);
        }

        var now = clock.UtcNow;

        var adult = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            NationalId = nationalId,
            PromotionalConsent = request.PromotionalConsent,
            Gender = request.Gender,
            Email = email,
            Phone = phone,
            SecondaryPhone = secondaryPhone,
            PasswordHash = hasher.Hash(request.Password),
            UserStatusTypeId = SeedIds.UserStatusTypes.Pending,
            IsAdmin = false,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = now,
        };

        var otpCode = AccountTokens.Create();
        adult.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);

        await users.AddAsync(adult, ct);

        var pendingMinors = minorRequests
            .Select(minor => new User
            {
                FirstName = minor.FirstName.Trim(),
                LastName = minor.LastName.Trim(),
                BirthDate = minor.BirthDate,
                Gender = minor.Gender,
                ParentId = adult.Id,
                UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
                UserTypeId = SeedIds.UserTypes.Participant,
                CreatedAt = now,
            })
            .ToList();
        foreach (var child in pendingMinors)
        {
            await users.AddAsync(child, ct);
        }

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        await TrySendVerificationEmailAsync(adult, otpCode, ct);

        var createdAdult = await users.GetByIdWithDetailsAsync(adult.Id, ct);
        var children = await users.ListChildrenWithDetailsAsync(adult.Id, ct);
        var createdMinors = children.Select(child => child.ToResponse()).ToList();

        return new RegisterResponse(createdAdult!.ToResponse(), createdMinors);
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

            user.OtpLastSentAt = null;
            await uow.SaveChangesAsync(ct);
        }
    }
}
