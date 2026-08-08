using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
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

public sealed record RegisterCommand(RegisterRequest Request) : ICommand<Result<RegisterResponse>>;

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
    public async Task<Result<RegisterResponse>> HandleAsync(
        RegisterCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

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
            logger.LogError(ex, "Failed to send the verification email for user {UserId}", user.Id);

            user.OtpLastSentAt = null;
            await uow.SaveChangesAsync(ct);
        }
    }
}
