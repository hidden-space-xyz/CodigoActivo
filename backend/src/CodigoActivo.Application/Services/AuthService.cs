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
    IUserTypeRepository userTypes,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    AccountVerificationOptions verification,
    ApplicationOptions application,
    ILogger<AuthService> logger
) : IAuthService
{
    // Client-side route of the SPA's account-verification page (see the frontend router).
    private const string VerificationPath = "/verify-account";

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
            return Error.Unauthorized(ErrorCode.InvalidCredentials);

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked)
            return Error.Forbidden(ErrorCode.UserAccountBlocked);

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent)
            return Error.Forbidden(ErrorCode.UserAccountIsDependent);

        var selfHealed = false;
        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending)
        {
            if (verification.Required)
                return Error.Forbidden(ErrorCode.UserAccountPendingVerification);

            user.Verify(SeedIds.UserStatusTypes.Active);
            selfHealed = true;
        }

        user.RegisterLogin();
        await uow.SaveChangesAsync(ct);

        // Verify() only changes the status FK, not the loaded UserStatusType navigation, so the
        // self-healed response would otherwise carry the stale "Pending" name/color; reload it.
        if (selfHealed)
            return (await users.GetByIdWithDetailsAsync(user.Id, ct))!.ToResponse();

        return user.ToResponse();
    }

    public async Task<Result<UserResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdWithDetailsAsync(userId, ct);
        if (user is null)
            return Error.Unauthorized(ErrorCode.CurrentUserNotFound);

        return user.ToResponse();
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    )
    {
        if (request.BirthDate.IsMinor())
            return Error.BadRequest(ErrorCode.RegisterAdultCannotBeMinor);

        var isFirstUser = !await users.ExistsAsync(_ => true, ct);

        if (!isFirstUser)
        {
            var adultRole = await userTypes.FindAsync(ut => ut.Id == request.RoleId, ct);
            if (adultRole is null)
                return Error.NotFound(ErrorCode.UserTypeNotFound);

            if (adultRole.Hidden || !adultRole.IsAllowedForAdults)
                return Error.BadRequest(ErrorCode.UserTypeNotAllowedForAdults);
        }

        var email = request.Email.NormalizeEmailOrNull();
        var phone = request.Phone.NormalizeOrNull();
        if (email is null || phone is null || string.IsNullOrWhiteSpace(request.Password))
            return Error.BadRequest(ErrorCode.RegisterContactInfoRequired);

        if (await users.ExistsAsync(u => u.Email == email || u.Phone == phone, ct))
            return Error.Conflict(ErrorCode.RegisterEmailOrPhoneAlreadyInUse);

        var minorRequests = request.Minors ?? [];
        if (minorRequests.Any(minor => !minor.BirthDate.IsMinor()))
            return Error.BadRequest(ErrorCode.RegisterMinorBirthDateNotMinor);

        if (minorRequests.Count > 0)
        {
            var roleIds = minorRequests.Select(minor => minor.RoleId).Distinct().ToList();
            var minorRoles = await userTypes.GetAsync(ut => roleIds.Contains(ut.Id), ct);
            if (minorRoles.Count != roleIds.Count)
                return Error.NotFound(ErrorCode.UserTypeNotFound);

            if (minorRoles.Any(role => role.Hidden || !role.IsAllowedForMinors))
                return Error.BadRequest(ErrorCode.UserTypeNotAllowedForMinors);
        }

        var now = clock.UtcNow;

        var adult = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            Email = email,
            Phone = phone,
            PasswordHash = hasher.Hash(request.Password),
            UserStatusTypeId = verification.Required
                ? SeedIds.UserStatusTypes.Pending
                : SeedIds.UserStatusTypes.Active,
            IsAdmin = isFirstUser,
            UserTypeId = isFirstUser ? SeedIds.UserTypes.Member : request.RoleId,
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
                ParentId = adult.Id,
                UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
                UserTypeId = minor.RoleId,
                CreatedAt = now,
            };
            await users.AddAsync(child, ct);
        }

        await uow.SaveChangesAsync(ct);

        if (otpCode is not null)
            await TrySendVerificationEmailAsync(adult, otpCode, ct);

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
            return Error.NotFound(ErrorCode.UserNotFound);

        if (
            user.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
            || string.IsNullOrWhiteSpace(otp)
            || user.OtpCodeHash is null
            || user.OtpExpiresAt is null
            || user.OtpExpiresAt < clock.UtcNow
            || !hasher.Verify(NormalizeOtp(otp), user.OtpCodeHash)
        )
            return Error.BadRequest(ErrorCode.OtpInvalidOrExpired);

        user.Verify(SeedIds.UserStatusTypes.Active);
        await uow.SaveChangesAsync(ct);

        var updated = await users.GetByIdWithDetailsAsync(id, ct);
        return updated!.ToResponse();
    }

    public async Task<Result> ResendVerificationAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        if (
            !verification.Required
            || user.UserStatusTypeId != SeedIds.UserStatusTypes.Pending
            || string.IsNullOrWhiteSpace(user.Email)
        )
            return Error.Conflict(ErrorCode.OtpResendNotAllowed);

        var now = clock.UtcNow;
        if (
            user.OtpLastSentAt is not null
            && now < user.OtpLastSentAt + verification.ResendCooldown
        )
            return Error.Conflict(ErrorCode.OtpResendCooldownActive);

        var otpCode = Guid.NewGuid().ToString();
        await SendVerificationEmailAsync(user, otpCode, ct);

        user.IssueOtp(hasher.Hash(otpCode), now, verification.OtpLifetime);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The account is already persisted; the user can request a new code from the
            // verification screen, so a failed delivery must not fail the registration.
            logger.LogError(ex, "Failed to send the verification email for user {UserId}", user.Id);

            // OtpLastSentAt was stamped optimistically; since nothing was delivered, clear it so the
            // resend cooldown does not block the user's first retry.
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
            BuildVerificationUrl(user.Id, otpCode),
            verification.OtpLifetime
        );
        return emailSender.SendAsync(message, ct);
    }

    private string BuildVerificationUrl(Guid userId, string otpCode)
    {
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{application.BaseUrl.TrimEnd('/')}{VerificationPath}?userId={userId}&code={Uri.EscapeDataString(otpCode)}"
        );
    }

    // The OTP is a GUID (case-insensitive by convention) generated lowercase; normalize the
    // submitted value so a code retyped with different casing still matches.
    private static string NormalizeOtp(string otp)
    {
        return otp.Trim().ToLowerInvariant();
    }
}
