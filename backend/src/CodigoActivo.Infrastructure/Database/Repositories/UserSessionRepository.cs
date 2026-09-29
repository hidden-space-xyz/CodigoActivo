using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Stores the sessions opened by accounts.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class UserSessionRepository(CodigoActivoDbContext context)
    : AggregateRepository<UserSession>(context),
        IUserSessionRepository
{
    /// <inheritdoc />
    public Task EndAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        return Set.Where(session => session.Id == sessionId && session.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public Task EndAllAsync(Guid userId, CancellationToken ct = default)
    {
        return Set.Where(session => session.UserId == userId).ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> RemoveExpiredAsync(
        DateTimeOffset now,
        Guid? userId = null,
        CancellationToken ct = default
    )
    {
        var expired = Set.Where(session => session.ExpiresAt <= now);
        if (userId is { } owner)
        {
            expired = expired.Where(session => session.UserId == owner);
        }

        return expired.ExecuteDeleteAsync(ct);
    }
}
