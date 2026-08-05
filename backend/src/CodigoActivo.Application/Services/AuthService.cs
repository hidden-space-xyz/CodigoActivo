using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Services;

public class AuthService(
    IUserRepository users,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    AccountVerificationOptions verification,
    PasswordResetOptions passwordReset,
    ApplicationOptions application,
    ILogger<AuthService> logger,
    ICacheInvalidator cacheInvalidator
) : IAuthService
{
    private const string VerificationPath = "/verify-account";
    private const string PasswordResetPath = "/reset-password";

    public async Task<Result<UserResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default
    )
    {
        var identifier = request.Identifier.Trim();
        var user = await users.GetByEmailOrPhoneAsync(identifier, ct);

        if (
            user is null
            || string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(request.Password, user.PasswordHash)
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

        var selfHealed = false;
        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending)
        {
            if (verification.Required)
            {
                return Error.Forbidden(ErrorCode.UserAccountPendingVerification);
            }

            user.Verify(SeedIds.UserStatusTypes.Active, clock.UtcNow);
            selfHealed = true;
        }

        user.RegisterLogin(clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        return selfHealed
            ? (Result<UserResponse>)(await users.GetByIdWithDetailsAsync(user.Id, ct))!.ToResponse()
            : (Result<UserResponse>)user.ToResponse();
    }

    public async Task<Result<UserResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdWithDetailsAsync(userId, ct);
        return user is null
            ? (Result<UserResponse>)Error.Unauthorized(ErrorCode.CurrentUserNotFound)
            : (Result<UserResponse>)user.ToResponse();
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    )
    {
        var today = clock.Today;

        if (request.BirthDate.IsMinor(today))
        {
            return Error.BadRequest(ErrorCode.RegisterAdultCannotBeMinor);
        }

        var isFirstUser = !await users.ExistsAsync(_ => true, ct);

        var email = request.Email.NormalizeEmailOrNull();
        var phone = request.Phone.NormalizeOrNull();
        if (email is null || phone is null || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.BadRequest(ErrorCode.RegisterContactInfoRequired);
        }

        if (await users.ExistsAsync(u => u.Email == email || u.Phone == phone, ct))
        {
            return Error.Conflict(ErrorCode.RegisterEmailOrPhoneAlreadyInUse);
        }

        var minorRequests = request.Minors ?? [];
        if (minorRequests.Any(minor => !minor.BirthDate.IsMinor(today)))
        {
            return Error.BadRequest(ErrorCode.RegisterMinorBirthDateNotMinor);
        }

        var now = clock.UtcNow;

        var adult = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            Email = email,
            Phone = phone,
            PasswordHash = hasher.Hash(request.Password),
            UserStatusTypeId = verification.Required
                ? SeedIds.UserStatusTypes.Pending
                : SeedIds.UserStatusTypes.Active,
            IsAdmin = isFirstUser,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = now,
        };

        string? otpCode = null;
        if (verification.Required)
        {
            otpCode = Guid.NewGuid().ToString();
            adult.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);
        }

        await users.AddAsync(adult, ct);

        foreach (var minor in minorRequests)
        {
            var child = new User
            {
                FirstName = minor.FirstName.Trim(),
                LastName = minor.LastName.Trim(),
                BirthDate = minor.BirthDate,
                Gender = minor.Gender,
                ParentId = adult.Id,
                UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
                UserTypeId = SeedIds.UserTypes.Participant,
                CreatedAt = now,
            };
            await users.AddAsync(child, ct);
        }

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        if (otpCode is not null)
        {
            await TrySendVerificationEmailAsync(adult, otpCode, ct);
        }

        var createdAdult = await users.GetByIdWithDetailsAsync(adult.Id, ct);
        var children = await users.ListChildrenWithDetailsAsync(adult.Id, ct);
        var createdMinors = children.Select(child => child.ToResponse()).ToList();

        return new RegisterResponse(
            createdAdult!.ToResponse(),
            createdMinors,
            verification.Required
        );
    }

    public async Task<Result<UserResponse>> VerifyAsync(
        Guid id,
        string otp,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            user.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
            || !IsCodeValid(otp, user.OtpCodeHash, user.OtpExpiresAt)
        )
        {
            return Error.BadRequest(ErrorCode.OtpInvalidOrExpired);
        }

        user.Verify(SeedIds.UserStatusTypes.Active, clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        var updated = await users.GetByIdWithDetailsAsync(id, ct);
        return updated!.ToResponse();
    }

    public async Task<Result> ResendVerificationAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            !verification.Required
            || user.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
            || string.IsNullOrWhiteSpace(user.Email)
        )
        {
            return Error.Conflict(ErrorCode.OtpResendNotAllowed);
        }

        var now = clock.UtcNow;
        if (now < user.OtpLastSentAt + verification.ResendCooldown)
        {
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);
        }

        var otpCode = Guid.NewGuid().ToString();
        try
        {
            await SendVerificationEmailAsync(user, otpCode, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);
        }

        user.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken ct = default
    )
    {
        var email = request.Email.NormalizeEmailOrNull();
        if (email is null)
        {
            return Result.Success();
        }

        var user = await users.FindAsync(u => u.Email == email, ct);
        if (
            user is null
            || string.IsNullOrEmpty(user.PasswordHash)
            || user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked
            || user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
        )
        {
            return Result.Success();
        }

        var now = clock.UtcNow;
        if (now < user.PasswordResetLastSentAt + passwordReset.ResendCooldown)
        {
            return Result.Success();
        }

        var code = Guid.NewGuid().ToString();
        try
        {
            await SendPasswordResetEmailAsync(user, code, ct);
        }
        catch (EmailRateLimitedException)
        {
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send the password reset email for user {UserId}",
                user.Id
            );
            return Result.Success();
        }

        user.IssuePasswordResetCode(hasher.Hash(code), now, passwordReset.CodeLifetime);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        Guid id,
        ResetPasswordRequest request,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        if (
            user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked
            || user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
            || !IsCodeValid(request.Otp, user.PasswordResetCodeHash, user.PasswordResetExpiresAt)
        )
        {
            return Error.BadRequest(ErrorCode.PasswordResetInvalidOrExpired);
        }

        user.ResetPassword(hasher.Hash(request.NewPassword), clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    private async Task TrySendVerificationEmailAsync(
        User user,
        string otpCode,
        CancellationToken ct
    )
    {
        try
        {
            await SendVerificationEmailAsync(user, otpCode, ct);
        }
        catch (EmailRateLimitedException)
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to send the verification email for user {UserId}", user.Id);

            user.OtpLastSentAt = null;
            await uow.SaveChangesAsync(ct);
        }
    }

    private Task SendVerificationEmailAsync(User user, string otpCode, CancellationToken ct)
    {
        var message = VerificationEmail.Create(
            user.Email!,
            user.FirstName,
            otpCode,
            BuildAccountUrl(VerificationPath, user.Id, otpCode),
            BuildSiteUrl(),
            verification.OtpLifetime
        );
        return emailSender.SendAsync(message, ct);
    }

    private Task SendPasswordResetEmailAsync(User user, string code, CancellationToken ct)
    {
        var message = PasswordResetEmail.Create(
            user.Email!,
            user.FirstName,
            BuildAccountUrl(PasswordResetPath, user.Id, code),
            BuildSiteUrl(),
            passwordReset.CodeLifetime
        );
        return emailSender.SendAsync(message, ct);
    }

    private string BuildSiteUrl()
    {
        return application.BaseUrl.TrimEnd('/');
    }

    private string BuildAccountUrl(string path, Guid userId, string code)
    {
        return $"{BuildSiteUrl()}{path}?userId={userId}&code={Uri.EscapeDataString(code)}";
    }

    private bool IsCodeValid(string code, string? codeHash, DateTimeOffset? expiresAt)
    {
        return !string.IsNullOrWhiteSpace(code)
            && codeHash is not null
            && expiresAt >= clock.UtcNow
            && hasher.Verify(code.Trim().ToLowerInvariant(), codeHash);
    }
}
