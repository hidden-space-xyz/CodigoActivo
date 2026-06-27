using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Services;

public class UserService(
    IUserRepository users,
    IUserTypeRepository userTypes,
    IUserStatusTypeRepository userStatusTypes,
    IUnitOfWork uow
) : IUserService
{
    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await users.ListWithDetailsAsync(ct);
        return list.Select(u => u.ToResponse()).ToList();
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.GetByIdWithDetailsAsync(id, ct);
        var response = user?.ToResponse();

        return response is null ? Error.NotFound() : Result.Success(response);
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

    public async Task<IReadOnlyList<UserStatusTypeResponse>> GetUserStatusTypesAsync(
        CancellationToken ct = default
    )
    {
        var list = await userStatusTypes.GetAllAsync(ct);
        return list.OrderBy(x => x.Name).Select(statusType => statusType.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<UserTypeResponse>> GetUserTypesAsync(
        CancellationToken ct = default
    )
    {
        var list = await userTypes.GetAllAsync(ct);
        return list.OrderBy(x => x.Name).Select(userType => userType.ToResponse()).ToList();
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
