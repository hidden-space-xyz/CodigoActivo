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
    private static readonly SortMap<User> Sort = new SortMap<User>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("email", u => u.Email)
        .Add("phone", u => u.Phone)
        .Add("createdAt", u => u.CreatedAt)
        .Add("birthDate", u => u.BirthDate)
        .Add("status", u => u.UserStatusType.Name)
        .Add("type", u => u.UserType.Name)
        .Add("isAdmin", u => u.IsAdmin)
        .Add("parentName", u => u.Parent!.FirstName)
        .Add("dependents", u => u.Children.Count)
        .Default("firstName")
        .Tie(u => u.Id);

    public Task<PagedResult<UserResponse>> ListAsync(
        UserListQuery query,
        Guid callerId,
        bool isAdmin,
        CancellationToken ct = default
    )
    {
        var source = users.Query();

        if (!isAdmin)
            source = source.Where(u => u.Id == callerId || u.ParentId == callerId);

        if (query.Id is { } id)
            source = source.Where(u => u.Id == id);
        if (query.ParentId is { } parentId)
            source = source.Where(u => u.ParentId == parentId);
        if (query.UserTypeId is { } userTypeId)
            source = source.Where(u => u.UserTypeId == userTypeId);
        if (query.UserStatusTypeId is { } userStatusTypeId)
            source = source.Where(u => u.UserStatusTypeId == userStatusTypeId);
        if (query.IsAdmin is { } admin)
            source = source.Where(u => u.IsAdmin == admin);
        if (query.BirthDateFrom is { } birthDateFrom)
            source = source.Where(u => u.BirthDate >= birthDateFrom);
        if (query.BirthDateTo is { } birthDateTo)
            source = source.Where(u => u.BirthDate <= birthDateTo);
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            source = source.Where(
                TextSearch.Contains<User>(
                    u => u.FirstName + " " + u.LastName,
                    TextSearch.Normalize(query.Name)
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(query.Email))
        {
            source = source.Where(
                TextSearch.Contains<User>(u => u.Email, TextSearch.Normalize(query.Email))
            );
        }

        if (!string.IsNullOrWhiteSpace(query.Phone))
        {
            source = source.Where(
                TextSearch.Contains<User>(u => u.Phone, TextSearch.Normalize(query.Phone))
            );
        }

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(
            source.Select(isAdmin ? Projections.UserWithType : Projections.User),
            query.Page,
            query.PageSize,
            ct
        );
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == id).Select(Projections.UserWithType),
            ct
        );
        return response is null
            ? (Result<UserResponse>)Error.NotFound(ErrorCode.UserNotFound)
            : (Result<UserResponse>)response;
    }

    public async Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        var rules = request.BirthDate.IsMinor(clock.Today)
            ? await ApplyMinorContactRulesAsync(user, request.ParentId, id, ct)
            : await ApplyAdultContactRulesAsync(
                user,
                request.Email,
                request.Phone,
                request.ParentId,
                id,
                ct
            );
        if (rules.IsFailure)
            return rules.Error!;

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.BirthDate = request.BirthDate;
        user.UpdatedAt = clock.UtcNow;

        await uow.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        if (user.IsAdmin)
            return Error.Forbidden(ErrorCode.UserDeleteAdminForbidden);

        users.Remove(user);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetAdminAsync(Guid id, bool isAdmin, CancellationToken ct = default)
    {
        var user = await users.FindAsync(u => u.Id == id, ct);
        if (user is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        if (user.IsAdmin == isAdmin)
            return Result.Success();

        if (!isAdmin && await users.CountAsync(u => u.IsAdmin, ct) <= 1)
            return Error.Forbidden(ErrorCode.UserCannotRemoveLastAdmin);

        user.IsAdmin = isAdmin;
        user.UpdatedAt = clock.UtcNow;
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
        if (user is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        if (!await userTypes.ExistsAsync(ut => ut.Id == userTypeId, ct))
            return Error.NotFound(ErrorCode.UserTypeNotFound);

        if (user.UserTypeId != userTypeId)
        {
            user.UserTypeId = userTypeId;
            user.UpdatedAt = clock.UtcNow;
            await uow.SaveChangesAsync(ct);
        }

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result<UserResponse>> AddChildAsync(
        Guid parentId,
        RegisterMinorRequest request,
        CancellationToken ct = default
    )
    {
        var parent = await users.FindAsync(u => u.Id == parentId, ct);
        if (parent is null)
            return Error.NotFound(ErrorCode.ParentUserNotFound);

        var today = clock.Today;

        if (parent.BirthDate.IsMinor(today))
            return Error.BadRequest(ErrorCode.UserParentIsMinor);

        if (!request.BirthDate.IsMinor(today))
            return Error.BadRequest(ErrorCode.UserChildBirthDateNotMinor);

        var now = clock.UtcNow;
        var child = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            ParentId = parentId,
            UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = now,
        };
        await users.AddAsync(child, ct);
        await uow.SaveChangesAsync(ct);

        return await GetByIdAsync(child.Id, ct);
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken ct = default
    )
    {
        var user = await users.FindAsync(u => u.Id == userId, ct);
        if (user is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        if (string.IsNullOrEmpty(user.PasswordHash))
            return Error.BadRequest(ErrorCode.UserPasswordNotSet);

        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.UpdatedAt = clock.UtcNow;
        await uow.SaveChangesAsync(ct);
        return Result.Success();
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
        if (parentId is not { } parent)
            return Error.BadRequest(ErrorCode.UserParentIdRequired);

        if (parent == excludeUserId)
            return Error.BadRequest(ErrorCode.UserCannotBeOwnParent);

        var parentUser = await users.FindAsync(u => u.Id == parent, ct);
        if (parentUser is null)
            return Error.NotFound(ErrorCode.ParentUserNotFound);

        if (parentUser.BirthDate.IsMinor(clock.Today))
            return Error.BadRequest(ErrorCode.UserParentIsMinor);

        if (user.ParentId is { } currentParent && currentParent != parent)
            return Error.Forbidden(ErrorCode.UserParentReassignmentForbidden);

        user.ParentId = parent;
        user.Email = null;
        user.Phone = null;
        user.PasswordHash = null;
        user.ClearOtp();
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
            return Error.BadRequest(ErrorCode.UserParentNotAllowedForAdult);

        var email = rawEmail.NormalizeEmailOrNull();
        var phone = rawPhone.NormalizeOrNull();
        if (email is null || phone is null)
            return Error.BadRequest(ErrorCode.UserContactInfoRequired);

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
