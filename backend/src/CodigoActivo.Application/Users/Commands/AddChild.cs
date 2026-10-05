using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the input required to add a child.
/// </summary>
/// <param name="ParentId">Identifier of the parent.</param>
/// <param name="Request">Validated client request data.</param>
public sealed record AddChildCommand(Guid ParentId, RegisterMinorRequest Request)
    : ICommand<Result<Guid>>;

/// <summary>
/// Executes the command to add a child. A guardian has at most
/// <see cref="Household.MaxDependents"/> dependents; they are counted with the guardian's row
/// locked, so parallel requests cannot pass the limit together.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="cacheInvalidator">Service used to invalidate stale cached responses.</param>
public sealed class AddChildCommandHandler(
    IUserRepository users,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<AddChildCommand, Result<Guid>>
{
    /// <summary>
    /// Handles the request to add a child.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
        AddChildCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var parent = await users.GetByIdAsync(command.ParentId, ct);
        if (parent is null)
        {
            return Error.NotFound(ErrorCode.ParentUserNotFound);
        }

        var child = User.CreateDependent(
            parent,
            new PersonDetails(
                request.FirstName,
                request.LastName,
                request.Gender,
                BirthDate: request.BirthDate
            ),
            clock.Today,
            clock.UtcNow
        );
        if (child.IsFailure)
        {
            return child.Error!;
        }

        var added = await uow.ExecuteInTransactionAsync(
            attempt => AddLockedAsync(parent, child.Value, attempt),
            ct
        );
        if (added.IsFailure)
        {
            return added.Error!;
        }

        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        return child.Value.Id;
    }

    private async Task<Result> AddLockedAsync(User parent, User child, CancellationToken ct)
    {
        if (!await users.LockAsync(parent, ct))
        {
            return Error.NotFound(ErrorCode.ParentUserNotFound);
        }

        var allowed = Household.EnsureMayAddDependent(
            await users.CountDependentsAsync(parent.Id, ct)
        );
        if (allowed.IsFailure)
        {
            return allowed;
        }

        await users.AddAsync(child, ct);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
