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

public class UserService(
    IUserRepository users,
    IUserTypeRepository userTypes,
    IUserStatusTypeRepository userStatusTypes,
    IPasswordHasher hasher,
    IUnitOfWork uow
) : IUserService
{
    public IQueryable<UserResponse> QueryUsers()
    {
        return users.Query().Select(Projections.User);
    }

    public async Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return Error.NotFound();
        }

        var rules = request.BirthDate.IsMinor()
            ? await ApplyMinorContactRulesAsync(user, request.ParentId, id, ct)
            : await ApplyAdultContactRulesAsync(
                user,
                request.Email,
                request.Phone,
                request.ParentId,
                excludeUserId: id,
                ct
            );
        if (rules.IsFailure)
        {
            return rules.Error!;
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.BirthDate = request.BirthDate;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        users.Update(user);
        await uow.SaveChangesAsync(ct);

        var updated = await users.GetByIdWithDetailsAsync(id, ct);
        return updated!.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await users.ExistsAsync(u => u.Id == id, ct))
        {
            return Error.NotFound();
        }

        if (await users.HasTypeAssignmentAsync(id, SeedIds.UserTypes.Admin, ct))
        {
            return Error.Forbidden();
        }

        await users.RemoveAsync(u => u.Id == id, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<UserResponse>> ChangeTypeAsync(
        Guid id,
        Guid userTypeId,
        CancellationToken ct = default
    )
    {
        if (!await users.ExistsAsync(u => u.Id == id, ct))
        {
            return Error.NotFound();
        }

        if (!await userTypes.ExistsAsync(ut => ut.Id == userTypeId, ct))
        {
            return Error.NotFound();
        }

        if (!await users.HasTypeAssignmentAsync(id, userTypeId, ct))
        {
            await users.AddTypeAssignmentAsync(
                new UserTypeAssignment
                {
                    UserId = id,
                    UserTypeId = userTypeId,
                    AssignedAt = DateTimeOffset.UtcNow,
                },
                ct
            );
            await uow.SaveChangesAsync(ct);
        }

        var updated = await users.GetByIdWithDetailsAsync(id, ct);
        return updated!.ToResponse();
    }

    public async Task<Result<UserResponse>> AddChildAsync(
        Guid parentId,
        RegisterMinorRequest request,
        CancellationToken ct = default
    )
    {
        var parent = await users.FindAsync(u => u.Id == parentId, ct);
        if (parent is null)
        {
            return Error.NotFound();
        }

        if (parent.BirthDate.IsMinor())
        {
            return Error.Validation();
        }

        if (!request.BirthDate.IsMinor())
        {
            return Error.Validation();
        }

        var role = await userTypes.FindAsync(ut => ut.Id == request.RoleId, ct);
        if (role is null)
        {
            return Error.NotFound();
        }

        if (role.Hidden || !role.IsAllowedForMinors)
        {
            return Error.Validation();
        }

        var now = DateTimeOffset.UtcNow;
        var child = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            ParentId = parentId,
            UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
            CreatedAt = now,
        };
        await users.AddAsync(child, ct);
        await users.AddTypeAssignmentAsync(
            new UserTypeAssignment
            {
                UserId = child.Id,
                UserTypeId = request.RoleId,
                AssignedAt = now,
            },
            ct
        );
        await uow.SaveChangesAsync(ct);

        var created = await users.GetByIdWithDetailsAsync(child.Id, ct);
        return created!.ToResponse();
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return Error.NotFound();
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return Error.Validation();
        }

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Error.Validation();
        }

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        users.Update(user);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public IQueryable<RegistrationTypeResponse> QueryRegistrationTypes()
    {
        return userTypes.Query().Where(type => !type.Hidden).Select(Projections.RegistrationType);
    }

    public IQueryable<UserStatusTypeResponse> QueryStatusTypes()
    {
        return userStatusTypes.Query().Select(Projections.UserStatusType);
    }

    public IQueryable<UserTypeResponse> QueryUserTypes()
    {
        return userTypes.Query().Select(Projections.UserType);
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
