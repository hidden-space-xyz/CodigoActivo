using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to update the user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="ActingUserId">Identifier of the acting user.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record UpdateUserCommand(Guid UserId, Guid ActingUserId, UpdateUserRequest Request)
    : ICommand<Result<UserResponse>>;

/// <summary>
/// Executes the command to update the user.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="hasher">Hasher used to verify the acting caller's password.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve user by identifier.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class UpdateUserCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetUserByIdQueryHandler getById,
    AccountSecurityNotifier securityNotifier,
    ILogger<UpdateUserCommandHandler> logger
) : ICommandHandler<UpdateUserCommand, Result<UserResponse>>
{
    private const string Operation = "UpdateUser";

    /// <summary>
    /// Handles the request to update the user. Replacing the login identifiers of the account, or
    /// dropping them by turning it into a dependent minor, first re-authenticates the acting
    /// caller, so a hijacked session alone cannot take the account over.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a user on success, or an application error on failure.</returns>
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

        var previousEmail = user.Email;
        var previousPhone = user.Phone;

        var rules = request.BirthDate.IsMinor(clock.Today)
            ? await ApplyMinorContactRulesAsync(command, user, ct)
            : await ApplyAdultContactRulesAsync(command, user, ct);
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

        if (
            previousEmail is not null
            && (
                !string.Equals(previousEmail, user.Email, StringComparison.Ordinal)
                || !string.Equals(previousPhone, user.Phone, StringComparison.Ordinal)
            )
        )
        {
            logger.LoginIdentifiersChanged(command.ActingUserId, user.Id);
            await securityNotifier.NotifyIdentifiersChangedAsync(
                user.Id,
                previousEmail,
                user.FirstName,
                user.Email,
                ct
            );
        }

        return await getById.HandleAsync(new GetUserByIdQuery(command.UserId), ct);
    }

    private async Task<Result> ApplyMinorContactRulesAsync(
        UpdateUserCommand command,
        User user,
        CancellationToken ct
    )
    {
        if (command.Request.ParentId is not { } parent)
        {
            return Error.BadRequest(ErrorCode.UserParentIdRequired);
        }

        if (parent == command.UserId)
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

        var dropsAccessFactors =
            user.Email is not null || user.Phone is not null || user.PasswordHash is not null;
        if (dropsAccessFactors && !await IsActingPasswordValidAsync(command, ct))
        {
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        user.ParentId = parent;
        user.Email = null;
        user.Phone = null;
        user.PasswordHash = null;
        user.ClearOtp();
        return Result.Success();
    }

    private async Task<Result> ApplyAdultContactRulesAsync(
        UpdateUserCommand command,
        User user,
        CancellationToken ct
    )
    {
        var request = command.Request;
        if (request.ParentId is not null)
        {
            return Error.BadRequest(ErrorCode.UserParentNotAllowedForAdult);
        }

        var email = request.Email.NormalizeEmailOrNull();
        var phone = request.Phone.NormalizeOrNull();
        if (email is null || phone is null)
        {
            return Error.BadRequest(ErrorCode.UserContactInfoRequired);
        }

        var replacesLoginIdentifiers =
            !string.Equals(email, user.Email, StringComparison.Ordinal)
            || !string.Equals(phone, user.Phone, StringComparison.Ordinal);
        if (replacesLoginIdentifiers && !await IsActingPasswordValidAsync(command, ct))
        {
            return Error.BadRequest(ErrorCode.UserCurrentPasswordIncorrect);
        }

        if (await users.EmailExistsAsync(email, command.UserId, ct))
        {
            return Error.Conflict(ErrorCode.UserEmailAlreadyInUse);
        }

        if (await users.PhoneExistsAsync(phone, command.UserId, ct))
        {
            return Error.Conflict(ErrorCode.UserPhoneAlreadyInUse);
        }

        user.ParentId = null;
        user.Email = email;
        user.Phone = phone;
        return Result.Success();
    }

    private async Task<bool> IsActingPasswordValidAsync(
        UpdateUserCommand command,
        CancellationToken ct
    )
    {
        var valid = await VerifyActingPasswordAsync(command, ct);
        if (!valid)
        {
            logger.ReauthenticationRejected(command.ActingUserId, Operation);
        }

        return valid;
    }

    private async Task<bool> VerifyActingPasswordAsync(
        UpdateUserCommand command,
        CancellationToken ct
    )
    {
        var password = command.Request.CurrentPassword;
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var actingUser = await users.FindAsync(u => u.Id == command.ActingUserId, ct);
        return !string.IsNullOrEmpty(actingUser?.PasswordHash)
            && hasher.Verify(password, actingUser.PasswordHash);
    }
}
