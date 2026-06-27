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

    public async Task<Result<CreateUserResponse>> RegisterAsync(
        CreateUserRequest request,
        CancellationToken ct = default
    )
    {
        var isFirstUser = !await users.ExistsAsync(_ => true, ct);

        if (!isFirstUser)
        {
            if (request.RoleId == SeedIds.UserTypes.Admin)
            {
                return Error.Validation();
            }

            if (!await userTypes.ExistsAsync(ut => ut.Id == request.RoleId, ct))
            {
                return Error.NotFound();
            }
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var configured = request.BirthDate.IsMinor()
            ? await ConfigureMinorAsync(user, request, ct)
            : await ConfigureAdultAsync(user, request, excludeUserId: null, ct);
        if (configured.IsFailure)
        {
            return configured.Error!;
        }

        var roleIds = isFirstUser
            ? new[] { SeedIds.UserTypes.Admin, SeedIds.UserTypes.Member }
            : [request.RoleId];

        await users.AddAsync(user, ct);
        foreach (var roleId in roleIds)
        {
            await users.AddTypeAssignmentAsync(
                new UserTypeAssignment
                {
                    UserId = user.Id,
                    UserTypeId = roleId,
                    AssignedAt = DateTimeOffset.UtcNow,
                },
                ct
            );
        }
        await uow.SaveChangesAsync(ct);

        var created = await users.GetByIdWithDetailsAsync(user.Id, ct);
        return new CreateUserResponse(created!.ToResponse(), configured.Value);
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

    private async Task<Result<Guid?>> ConfigureMinorAsync(
        User user,
        CreateUserRequest request,
        CancellationToken ct
    )
    {
        var rules = await ApplyMinorContactRulesAsync(
            user,
            request.ParentId,
            excludeUserId: user.Id,
            ct
        );
        if (rules.IsFailure)
        {
            return rules.Error!;
        }

        user.UserStatusTypeId = SeedIds.UserStatusTypes.Dependent;
        return Result.Success<Guid?>(null);
    }

    private async Task<Result<Guid?>> ConfigureAdultAsync(
        User user,
        CreateUserRequest request,
        Guid? excludeUserId,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation();
        }

        var rules = await ApplyAdultContactRulesAsync(
            user,
            request.Email,
            request.Phone,
            request.ParentId,
            excludeUserId,
            ct
        );
        if (rules.IsFailure)
        {
            return rules.Error!;
        }

        user.PasswordHash = hasher.Hash(request.Password);
        user.UserStatusTypeId = SeedIds.UserStatusTypes.Pending;

        var otp = Guid.NewGuid();
        user.OtpCode = otp;
        user.OtpExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        return Result.Success<Guid?>(otp);
    }

    private async Task<Result> ApplyMinorContactRulesAsync(
        User user,
        Guid? parentId,
        Guid? excludeUserId,
        CancellationToken ct
    )
    {
        if (parentId is not { } parent)
        {
            return Error.Validation();
        }

        if (parent == excludeUserId)
        {
            return Error.Validation();
        }

        var parentUser = await users.FindAsync(u => u.Id == parent, ct);
        if (parentUser is null)
        {
            return Error.NotFound();
        }

        if (parentUser.BirthDate.IsMinor())
        {
            return Error.Validation();
        }

        user.ParentId = parent;
        user.Email = null;
        user.Phone = null;
        user.PasswordHash = null;
        user.OtpCode = null;
        user.OtpExpiresAt = null;
        return Result.Success();
    }

    private async Task<Result> ApplyAdultContactRulesAsync(
        User user,
        string? rawEmail,
        string? rawPhone,
        Guid? parentId,
        Guid? excludeUserId,
        CancellationToken ct
    )
    {
        if (parentId is not null)
        {
            return Error.Validation();
        }

        var email = rawEmail.NormalizeOrNull();
        var phone = rawPhone.NormalizeOrNull();
        if (email is null || phone is null)
        {
            return Error.Validation();
        }

        if (await users.EmailExistsAsync(email, excludeUserId, ct))
        {
            return Error.Validation();
        }

        if (await users.PhoneExistsAsync(phone, excludeUserId, ct))
        {
            return Error.Validation();
        }

        user.ParentId = null;
        user.Email = email;
        user.Phone = phone;
        return Result.Success();
    }
}
