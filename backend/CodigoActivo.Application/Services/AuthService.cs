using System.Security.Cryptography;
using System.Text;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Services;

public class AuthService(
    IUserRepository users,
    IUserTypeRepository userTypes,
    IUnitOfWork uow,
    IClock clock,
    IPasswordHasher hasher
) : IAuthService
{
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

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending)
            return Error.Forbidden(ErrorCode.UserAccountPendingVerification);

        user.RegisterLogin();
        await uow.SaveChangesAsync(ct);

        return user.ToResponse();
    }

    public async Task<Result<UserResponse>> GetCurrentAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var user = await users.GetByIdWithDetailsAsync(userId, ct);
        if (user is null) return Error.Unauthorized(ErrorCode.CurrentUserNotFound);

        return user.ToResponse();
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    )
    {
        if (request.BirthDate.IsMinor()) return Error.BadRequest(ErrorCode.RegisterAdultCannotBeMinor);

        var isFirstUser = !await users.ExistsAsync(_ => true, ct);

        if (!isFirstUser)
        {
            var adultRole = await userTypes.FindAsync(ut => ut.Id == request.RoleId, ct);
            if (adultRole is null) return Error.NotFound(ErrorCode.UserTypeNotFound);

            if (adultRole.Hidden || !adultRole.IsAllowedForAdults)
                return Error.BadRequest(ErrorCode.UserTypeNotAllowedForAdults);
        }

        var email = request.Email.NormalizeOrNull();
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
            if (minorRoles.Count != roleIds.Count) return Error.NotFound(ErrorCode.UserTypeNotFound);

            if (minorRoles.Any(role => role.Hidden || !role.IsAllowedForMinors))
                return Error.BadRequest(ErrorCode.UserTypeNotAllowedForMinors);
        }

        var now = clock.UtcNow;
        var otp = Guid.NewGuid();

        var adult = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            Email = email,
            Phone = phone,
            PasswordHash = hasher.Hash(request.Password),
            UserStatusTypeId = SeedIds.UserStatusTypes.Pending,
            OtpCode = otp,
            OtpExpiresAt = now.AddMinutes(15),
            CreatedAt = now,
        };
        await users.AddAsync(adult, ct);

        var adultRoleIds = isFirstUser
            ? new[] { SeedIds.UserTypes.Admin, SeedIds.UserTypes.Member }
            : [request.RoleId];
        foreach (var roleId in adultRoleIds)
            await users.AddTypeAssignmentAsync(
                new UserTypeAssignment
                {
                    UserId = adult.Id,
                    UserTypeId = roleId,
                    AssignedAt = now,
                },
                ct
            );

        foreach (var minor in minorRequests)
        {
            var child = new User
            {
                FirstName = minor.FirstName.Trim(),
                LastName = minor.LastName.Trim(),
                BirthDate = minor.BirthDate,
                ParentId = adult.Id,
                UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
                CreatedAt = now,
            };
            await users.AddAsync(child, ct);
            await users.AddTypeAssignmentAsync(
                new UserTypeAssignment
                {
                    UserId = child.Id,
                    UserTypeId = minor.RoleId,
                    AssignedAt = now,
                },
                ct
            );
        }

        await uow.SaveChangesAsync(ct);

        var createdAdult = await users.GetByIdWithDetailsAsync(adult.Id, ct);
        var children = await users.ListChildrenWithDetailsAsync(adult.Id, ct);
        var createdMinors = children.Select(child => child.ToResponse()).ToList();

        return new RegisterResponse(createdAdult!.ToResponse(), createdMinors, otp);
    }

    public async Task<Result<UserResponse>> VerifyAsync(
        Guid id,
        string otp,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null) return Error.NotFound(ErrorCode.UserNotFound);

        if (
            string.IsNullOrWhiteSpace(otp)
            || user.OtpCode == Guid.Empty || user.OtpCode is null
            || user.OtpExpiresAt is null
            || user.OtpExpiresAt < clock.UtcNow
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(user.OtpCode.Value.ToString()),
                Encoding.UTF8.GetBytes(otp)
            )
        )
            return Error.BadRequest(ErrorCode.OtpInvalidOrExpired);

        user.Verify(SeedIds.UserStatusTypes.Active);
        await uow.SaveChangesAsync(ct);

        var updated = await users.GetByIdWithDetailsAsync(id, ct);
        return updated!.ToResponse();
    }
}