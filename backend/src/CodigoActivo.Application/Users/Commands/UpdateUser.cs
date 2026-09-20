using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

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
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
/// <param name="getById">Handler used to retrieve user by identifier.</param>
/// <param name="securityNotifier">Notifier that warns the owner about credential changes.</param>
public sealed class UpdateUserCommandHandler(
    IUserRepository users,
    PasswordAttemptGuard passwordAttempts,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetUserByIdQueryHandler getById,
    AccountSecurityNotifier securityNotifier
) : ICommandHandler<UpdateUserCommand, Result<UserResponse>>
{
    /// <summary>
    /// Handles the request to update the user. The stored account decides which rules apply, never
    /// the request: an account that is not a dependent can neither become a minor nor be given a
    /// guardian, and a dependent keeps the guardian it already has, including after its birth date
    /// turns it into an adult. Replacing the login identifiers of the account first re-authenticates
    /// the acting caller, so a hijacked session alone cannot take the account over. Dependents are
    /// created only through <c>POST /api/users/{id}/children</c>, and they leave their guardian only
    /// when the guardian deletes them.
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

        var rules = user.ParentId is null
            ? await ApplyStandaloneAccountRulesAsync(command, user, ct)
            : ApplyDependentRules(request, user);
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

        var emailChanged = !string.Equals(previousEmail, user.Email, StringComparison.Ordinal);
        var phoneChanged = !string.Equals(previousPhone, user.Phone, StringComparison.Ordinal);
        if ((emailChanged || phoneChanged) && previousEmail is not null)
        {
            await securityNotifier.NotifyIdentifiersChangedAsync(
                previousEmail,
                user.FirstName,
                emailChanged ? user.Email : null,
                ct
            );
        }

        return await getById.HandleAsync(new GetUserByIdQuery(command.UserId), ct);
    }

    private async Task<Result> ApplyStandaloneAccountRulesAsync(
        UpdateUserCommand command,
        User user,
        CancellationToken ct
    )
    {
        var request = command.Request;
        if (request.BirthDate.IsMinor(clock.Today))
        {
            return Error.BadRequest(ErrorCode.UserCannotBecomeMinor);
        }

        if (request.ParentId is not null)
        {
            return Error.BadRequest(ErrorCode.UserParentNotAllowedForAdult);
        }

        return await ApplyLoginIdentifierRulesAsync(command, user, ct);
    }

    private static Result ApplyDependentRules(UpdateUserRequest request, User user)
    {
        if (request.ParentId is { } parent && parent != user.ParentId)
        {
            return Error.Forbidden(ErrorCode.UserParentReassignmentForbidden);
        }

        return Result.Success();
    }

    private async Task<Result> ApplyLoginIdentifierRulesAsync(
        UpdateUserCommand command,
        User user,
        CancellationToken ct
    )
    {
        var request = command.Request;
        var email = request.Email.NormalizeEmailOrNull();
        var phone = request.Phone.NormalizeOrNull();
        if (email is null || phone is null)
        {
            return Error.BadRequest(ErrorCode.UserContactInfoRequired);
        }

        var replacesLoginIdentifiers =
            !string.Equals(email, user.Email, StringComparison.Ordinal)
            || !string.Equals(phone, user.Phone, StringComparison.Ordinal);
        if (replacesLoginIdentifiers && !await VerifyActingPasswordAsync(command, ct))
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

        user.Email = email;
        user.Phone = phone;
        return Result.Success();
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
        return await passwordAttempts.VerifyReauthenticationAsync(actingUser, password, ct);
    }
}
