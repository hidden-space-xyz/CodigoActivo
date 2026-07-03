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
        return await Set.AsNoTracking()
            .Include(u => u.UserStatusType)
            .Include(u => u.TypeAssignments)
            .ThenInclude(ta => ta.UserType)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await Set.Include(u => u.UserStatusType)
            .Include(u => u.TypeAssignments)
            .ThenInclude(ta => ta.UserType)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default)
    {
        return await Set.Include(u => u.UserStatusType)
            .Include(u => u.TypeAssignments)
            .ThenInclude(ta => ta.UserType)
            .FirstOrDefaultAsync(u => u.Phone == phone, ct);
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

    public Task<bool> HasTypeAssignmentAsync(
        Guid userId,
        Guid userTypeId,
        CancellationToken ct = default
    )
    {
        return Context.UserTypeAssignments.AnyAsync(
            a => a.UserId == userId && a.UserTypeId == userTypeId,
            ct
        );
    }

    public async Task AddTypeAssignmentAsync(
        UserTypeAssignment assignment,
        CancellationToken ct = default
    )
    {
        await Context.UserTypeAssignments.AddAsync(assignment, ct);
    }

    public async Task<IReadOnlyList<User>> ListChildrenWithDetailsAsync(
        Guid parentId,
        CancellationToken ct = default
    )
    {
        return await Set.AsNoTracking()
            .Include(u => u.UserStatusType)
            .Include(u => u.TypeAssignments)
            .ThenInclude(ta => ta.UserType)
            .Where(u => u.ParentId == parentId)
            .OrderBy(u => u.FirstName)
            .ToListAsync(ct);
    }
}