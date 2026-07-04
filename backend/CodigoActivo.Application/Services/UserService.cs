using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
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
    IQueryExecutor executor,
    IClock clock,
    IUnitOfWork uow
) : IUserService
{
    private static readonly SortMap<UserResponse> Sort = new SortMap<UserResponse>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("createdAt", u => u.CreatedAt)
        .Add("birthDate", u => u.BirthDate)
        .Default("firstName")
        .Tie(u => u.Id);

    public Task<PagedResult<UserResponse>> ListAsync(
        UserListQuery query,
        Guid callerId,
        bool isAdmin,
        CancellationToken ct = default
    )
    {
        var source = users.Query().Select(Projections.User);

        // Row-level authorization: non-admins only ever see themselves and their dependents.
        if (!isAdmin)
            source = source.Where(u => u.Id == callerId || u.ParentId == callerId);

        if (query.ParentId is { } parentId) source = source.Where(u => u.ParentId == parentId);
        if (!string.IsNullOrWhiteSpace(query.FirstName))
            source = source.Where(
                TextSearch.Contains<UserResponse>(
                    u => u.FirstName,
                    TextSearch.Normalize(query.FirstName)
                )
            );
        if (!string.IsNullOrWhiteSpace(query.LastName))
            source = source.Where(
                TextSearch.Contains<UserResponse>(
                    u => u.LastName,
                    TextSearch.Normalize(query.LastName)
                )
            );
        if (!string.IsNullOrWhiteSpace(query.Email))
            source = source.Where(
                TextSearch.Contains<UserResponse>(u => u.Email, TextSearch.Normalize(query.Email))
            );

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == id).Select(Projections.User),
            ct
        );
        if (response is null) return Error.NotFound(ErrorCode.UserNotFound);
        return response;
    }

    public async Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null) return Error.NotFound(ErrorCode.UserNotFound);

        var rules = request.BirthDate.IsMinor()
            ? await ApplyMinorContactRulesAsync(user, request.ParentId, id, ct)
            : await ApplyAdultContactRulesAsync(
                user,
                request.Email,
                request.Phone,
                request.ParentId,
                id,
                ct
            );
        if (rules.IsFailure) return rules.Error!;

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.BirthDate = request.BirthDate;
        user.UpdatedAt = clock.UtcNow;

        await uow.SaveChangesAsync(ct);

        var updated = await users.GetByIdWithDetailsAsync(id, ct);
        return updated!.ToResponse();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (await users.HasTypeAssignmentAsync(id, SeedIds.UserTypes.Admin, ct))
            return Error.Forbidden(ErrorCode.UserDeleteAdminForbidden);

        if (await users.RemoveAsync(u => u.Id == id, ct) == 0) return Error.NotFound(ErrorCode.UserNotFound);

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<UserResponse>> ChangeTypeAsync(
        Guid id,
        Guid userTypeId,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null) return Error.NotFound(ErrorCode.UserNotFound);

        var role = await userTypes.FindAsync(ut => ut.Id == userTypeId, ct);
        if (role is null) return Error.NotFound(ErrorCode.UserTypeNotFound);

        var isMinor = user.BirthDate.IsMinor();
        if (role.Hidden || (isMinor ? !role.IsAllowedForMinors : !role.IsAllowedForAdults))
            return Error.BadRequest(
                isMinor ? ErrorCode.UserTypeNotAllowedForMinors : ErrorCode.UserTypeNotAllowedForAdults
            );

        if (!await users.HasTypeAssignmentAsync(id, userTypeId, ct))
        {
            await users.AddTypeAssignmentAsync(
                new UserTypeAssignment
                {
                    UserId = id,
                    UserTypeId = userTypeId,
                    AssignedAt = clock.UtcNow,
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
        if (parent is null) return Error.NotFound(ErrorCode.ParentUserNotFound);

        if (parent.BirthDate.IsMinor()) return Error.BadRequest(ErrorCode.UserParentIsMinor);

        if (!request.BirthDate.IsMinor()) return Error.BadRequest(ErrorCode.UserChildBirthDateNotMinor);

        var role = await userTypes.FindAsync(ut => ut.Id == request.RoleId, ct);
        if (role is null) return Error.NotFound(ErrorCode.UserTypeNotFound);

        if (role.Hidden || !role.IsAllowedForMinors) return Error.BadRequest(ErrorCode.UserTypeNotAllowedForMinors);

        var now = clock.UtcNow;
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
        if (user is null) return Error.NotFound(ErrorCode.UserNotFound);

        if (string.IsNullOrEmpty(user.PasswordHash)) return Error.BadRequest(ErrorCode.UserPasswordNotSet);

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.UpdatedAt = clock.UtcNow;
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<RegistrationTypeResponse>> ListRegistrationTypesAsync(
        RegistrationAudience? audience,
        CancellationToken ct = default
    )
    {
        var source = userTypes.Query().Where(type => !type.Hidden);

        source = audience switch
        {
            RegistrationAudience.Minor => source.Where(type => type.IsAllowedForMinors),
            RegistrationAudience.Adult => source.Where(type => type.IsAllowedForAdults),
            _ => source,
        };

        return await executor.ToListAsync(
            source.OrderBy(type => type.Name).Select(Projections.RegistrationType),
            ct
        );
    }

    public async Task<IReadOnlyList<UserStatusTypeResponse>> ListStatusTypesAsync(
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            userStatusTypes.Query().OrderBy(type => type.Name).Select(Projections.UserStatusType),
            ct
        );
    }

    public async Task<IReadOnlyList<UserTypeResponse>> ListUserTypesAsync(
        CancellationToken ct = default
    )
    {
        return await executor.ToListAsync(
            userTypes.Query().OrderBy(type => type.Name).Select(Projections.UserType),
            ct
        );
    }

    private async Task<Result> ApplyMinorContactRulesAsync(
        User user,
        Guid? parentId,
        Guid? excludeUserId,
        CancellationToken ct
    )
    {
        if (parentId is not { } parent) return Error.BadRequest(ErrorCode.UserParentIdRequired);

        if (parent == excludeUserId) return Error.BadRequest(ErrorCode.UserCannotBeOwnParent);

        var parentUser = await users.FindAsync(u => u.Id == parent, ct);
        if (parentUser is null) return Error.NotFound(ErrorCode.ParentUserNotFound);

        if (parentUser.BirthDate.IsMinor()) return Error.BadRequest(ErrorCode.UserParentIsMinor);

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
        if (parentId is not null) return Error.BadRequest(ErrorCode.UserParentNotAllowedForAdult);

        var email = rawEmail.NormalizeOrNull();
        var phone = rawPhone.NormalizeOrNull();
        if (email is null || phone is null) return Error.BadRequest(ErrorCode.UserContactInfoRequired);

        if (await users.EmailExistsAsync(email, excludeUserId, ct))
            return Error.Conflict(ErrorCode.UserEmailAlreadyInUse);

        if (await users.PhoneExistsAsync(phone, excludeUserId, ct))
            return Error.Conflict(ErrorCode.UserPhoneAlreadyInUse);

        user.ParentId = null;
        user.Email = email;
        user.Phone = phone;
        return Result.Success();
    }
}