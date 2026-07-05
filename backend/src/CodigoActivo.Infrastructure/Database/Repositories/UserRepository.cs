using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class UserRepository(CodigoActivoDbContext context)
    : Repository<User>(context),
        IUserRepository
{
    public async Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await QueryWithDetails().FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailOrPhoneAsync(string identifier, CancellationToken ct = default)
    {
        // Emails are stored lower-cased; fold the identifier so an email login is case-insensitive
        // (phones have no case, so the same folded value matches them too).
        var email = identifier.ToLowerInvariant();
        return await QueryWithDetails(tracked: true)
            .FirstOrDefaultAsync(u => u.Email == email || u.Phone == identifier, ct);
    }

    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            u => u.Email == email && (excludeUserId == null || u.Id != excludeUserId),
            ct
        );
    }

    public Task<bool> PhoneExistsAsync(
        string phone,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    )
    {
        return Set.AnyAsync(
            u => u.Phone == phone && (excludeUserId == null || u.Id != excludeUserId),
            ct
        );
    }

    public async Task<IReadOnlyList<User>> ListChildrenWithDetailsAsync(
        Guid parentId,
        CancellationToken ct = default
    )
    {
        return await QueryWithDetails()
            .Where(u => u.ParentId == parentId)
            .OrderBy(u => u.FirstName)
            .ToListAsync(ct);
    }

    private IQueryable<User> QueryWithDetails(bool tracked = false)
    {
        var query = tracked ? Set : Set.AsNoTracking();
        return query
            .Include(u => u.UserStatusType)
            .Include(u => u.UserType);
    }
}