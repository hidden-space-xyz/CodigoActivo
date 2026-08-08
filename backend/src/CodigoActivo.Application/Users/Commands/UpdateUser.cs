using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

public sealed record UpdateUserCommand(Guid UserId, UpdateUserRequest Request)
    : ICommand<Result<UserResponse>>;

public sealed class UpdateUserCommandHandler(
    IUserRepository users,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetUserByIdQueryHandler getById
) : ICommandHandler<UpdateUserCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> HandleAsync(
        UpdateUserCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var user = await users.FindAsync(u => u.Id == command.UserId, ct);
        if (user is null)
        {
            return Error.NotFound(ErrorCode.UserNotFound);
        }

        var rules = request.BirthDate.IsMinor(clock.Today)
            ? await ApplyMinorContactRulesAsync(user, request.ParentId, command.UserId, ct)
            : await ApplyAdultContactRulesAsync(
                user,
                request.Email,
                request.Phone,
                request.ParentId,
                command.UserId,
                ct
            );
        if (rules.IsFailure)
        {
            return rules.Error!;
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.BirthDate = request.BirthDate;
        user.Gender = request.Gender;
        user.UpdatedAt = clock.UtcNow;

        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        return await getById.HandleAsync(new GetUserByIdQuery(command.UserId), ct);
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
            return Error.BadRequest(ErrorCode.UserParentIdRequired);
        }

        if (parent == excludeUserId)
        {
            return Error.BadRequest(ErrorCode.UserCannotBeOwnParent);
        }

        var parentUser = await users.FindAsync(u => u.Id == parent, ct);
        if (parentUser is null)
        {
            return Error.NotFound(ErrorCode.ParentUserNotFound);
        }

        if (parentUser.BirthDate.IsMinor(clock.Today))
        {
            return Error.BadRequest(ErrorCode.UserParentIsMinor);
        }

        if (user.ParentId is { } currentParent && currentParent != parent)
        {
            return Error.Forbidden(ErrorCode.UserParentReassignmentForbidden);
        }

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
        {
            return Error.BadRequest(ErrorCode.UserParentNotAllowedForAdult);
        }

        var email = rawEmail.NormalizeEmailOrNull();
        var phone = rawPhone.NormalizeOrNull();
        if (email is null || phone is null)
        {
            return Error.BadRequest(ErrorCode.UserContactInfoRequired);
        }

        if (await users.EmailExistsAsync(email, excludeUserId, ct))
        {
            return Error.Conflict(ErrorCode.UserEmailAlreadyInUse);
        }

        if (await users.PhoneExistsAsync(phone, excludeUserId, ct))
        {
            return Error.Conflict(ErrorCode.UserPhoneAlreadyInUse);
        }

        user.ParentId = null;
        user.Email = email;
        user.Phone = phone;
        return Result.Success();
    }
}
