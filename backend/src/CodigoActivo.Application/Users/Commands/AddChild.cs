using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Commands;

public sealed record AddChildCommand(Guid ParentId, RegisterMinorRequest Request)
    : ICommand<Result<UserResponse>>;

public sealed class AddChildCommandHandler(
    IUserRepository users,
    IClock clock,
    IUnitOfWork uow,
    ICacheInvalidator cacheInvalidator,
    GetUserByIdQueryHandler getById
) : ICommandHandler<AddChildCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> HandleAsync(
        AddChildCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var parent = await users.FindAsync(u => u.Id == command.ParentId, ct);
        if (parent is null)
        {
            return Error.NotFound(ErrorCode.ParentUserNotFound);
        }

        var today = clock.Today;

        if (parent.BirthDate.IsMinor(today))
        {
            return Error.BadRequest(ErrorCode.UserParentIsMinor);
        }

        if (!request.BirthDate.IsMinor(today))
        {
            return Error.BadRequest(ErrorCode.UserChildBirthDateNotMinor);
        }

        var now = clock.UtcNow;
        var child = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            ParentId = command.ParentId,
            UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = now,
        };
        await users.AddAsync(child, ct);
        await uow.SaveChangesAsync(ct);
        await cacheInvalidator.InvalidateAsync(CacheTags.Users);

        return await getById.HandleAsync(new GetUserByIdQuery(child.Id), ct);
    }
}
