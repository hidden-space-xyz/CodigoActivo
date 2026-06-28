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
    IPasswordHasher hasher
) : IAuthService
{
    public async Task<Result<UserResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken ct = default
    )
    {
        var identifier = request.Identifier.Trim();
        var byEmail = identifier.Contains('@');

        User? user = byEmail
            ? await users.GetByEmailAsync(identifier, ct)
            : await users.GetByPhoneAsync(identifier, ct);
        user ??= byEmail
            ? await users.GetByPhoneAsync(identifier, ct)
            : await users.GetByEmailAsync(identifier, ct);

        if (
            user is null
            || string.IsNullOrEmpty(user.PasswordHash)
            || !hasher.Verify(request.Password, user.PasswordHash)
        )
        {
            return Error.Unauthorized();
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Blocked)
        {
            return Error.Forbidden();
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent)
        {
            return Error.Forbidden();
        }

        if (user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending)
        {
            return Error.Forbidden();
        }

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
        if (user is null)
        {
            return Error.Unauthorized();
        }

        return user.ToResponse();
    }

    public async Task<Result<RegisterResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    )
    {
        if (request.BirthDate.IsMinor())
        {
            return Error.Validation();
        }

        var isFirstUser = !await users.ExistsAsync(_ => true, ct);

        if (!isFirstUser)
        {
            var adultRole = await userTypes.FindAsync(ut => ut.Id == request.RoleId, ct);
            if (adultRole is null)
            {
                return Error.NotFound();
            }

            if (adultRole.Hidden || !adultRole.IsAllowedForAdults)
            {
                return Error.Validation();
            }
        }

        var email = request.Email.NormalizeOrNull();
        var phone = request.Phone.NormalizeOrNull();
        if (email is null || phone is null || string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation();
        }

        if (
            await users.EmailExistsAsync(email, null, ct)
            || await users.PhoneExistsAsync(phone, null, ct)
        )
        {
            return Error.Validation();
        }

        var minorRequests = request.Minors ?? [];
        foreach (var minor in minorRequests)
        {
            if (!minor.BirthDate.IsMinor())
            {
                return Error.Validation();
            }

            var minorRole = await userTypes.FindAsync(ut => ut.Id == minor.RoleId, ct);
            if (minorRole is null)
            {
                return Error.NotFound();
            }

            if (minorRole.Hidden || !minorRole.IsAllowedForMinors)
            {
                return Error.Validation();
            }
        }

        var now = DateTimeOffset.UtcNow;
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
        {
            await users.AddTypeAssignmentAsync(
                new UserTypeAssignment
                {
                    UserId = adult.Id,
                    UserTypeId = roleId,
                    AssignedAt = now,
                },
                ct
            );
        }

        var minorIds = new List<Guid>();
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
            minorIds.Add(child.Id);
        }

        await uow.SaveChangesAsync(ct);

        var createdAdult = await users.GetByIdWithDetailsAsync(adult.Id, ct);
        var createdMinors = new List<UserResponse>();
        foreach (var id in minorIds)
        {
            var created = await users.GetByIdWithDetailsAsync(id, ct);
            if (created is not null)
            {
                createdMinors.Add(created.ToResponse());
            }
        }

        return new RegisterResponse(createdAdult!.ToResponse(), createdMinors, otp);
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
            return Error.NotFound();
        }

        if (
            string.IsNullOrWhiteSpace(otp)
            || (user.OtpCode == Guid.Empty || user.OtpCode is null)
            || user.OtpExpiresAt is null
            || user.OtpExpiresAt < DateTimeOffset.UtcNow
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(user.OtpCode.Value.ToString()),
                System.Text.Encoding.UTF8.GetBytes(otp)
            )
        )
        {
            return Error.Validation();
        }

        user.Verify(SeedIds.UserStatusTypes.Active);
        users.Update(user);
        await uow.SaveChangesAsync(ct);

        var updated = await users.GetByIdWithDetailsAsync(id, ct);
        return updated!.ToResponse();
    }
}
