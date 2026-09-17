using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves user data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class UserRepository(CodigoActivoDbContext context)
    : Repository<User>(context),
        IUserRepository
{
    /// <summary>
    /// Gets the user by its identifier, including its related details.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user, or <see langword="null"/> when it is not found.</returns>
    public async Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await QueryWithDetails().FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    /// <summary>
    /// Gets the user identified by an email address or phone number.
    /// </summary>
    /// <param name="identifier">The identifier value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user, or <see langword="null"/> when it is not found.</returns>
    public async Task<User?> GetByEmailOrPhoneAsync(
        string identifier,
        CancellationToken ct = default
    )
    {
        var email = identifier.ToLowerInvariant();
        return await QueryWithDetails(tracked: true)
            .FirstOrDefaultAsync(u => u.Email == email || u.Phone == identifier, ct);
    }

    /// <summary>
    /// Determines whether an email already exists.
    /// </summary>
    /// <param name="email">Email address to validate or locate.</param>
    /// <param name="excludeUserId">Identifier of the user to exclude from the check.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Determines whether a phone already exists.
    /// </summary>
    /// <param name="phone">Phone number to validate or locate.</param>
    /// <param name="excludeUserId">Identifier of the user to exclude from the check.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Lists the children with details that match the supplied criteria.
    /// </summary>
    /// <param name="parentId">Identifier of the parent.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user items.</returns>
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

    /// <summary>
    /// Determines whether published content still credits the user or any minor under their
    /// guardianship as its author, uploader or last editor.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> HasAuthoredContentAsync(Guid userId, CancellationToken ct = default)
    {
        var household = Set.AsNoTracking()
            .Where(u => u.Id == userId || u.ParentId == userId)
            .Select(u => u.Id);

        return await Context
                .Activities.AsNoTracking()
                .AnyAsync(
                    a =>
                        household.Contains(a.CreatedBy)
                        || (a.UpdatedBy != null && household.Contains(a.UpdatedBy.Value)),
                    ct
                )
            || await Context
                .Announcements.AsNoTracking()
                .AnyAsync(
                    a =>
                        household.Contains(a.CreatedBy)
                        || (a.UpdatedBy != null && household.Contains(a.UpdatedBy.Value)),
                    ct
                )
            || await Context
                .Events.AsNoTracking()
                .AnyAsync(
                    e =>
                        household.Contains(e.CreatedBy)
                        || (e.UpdatedBy != null && household.Contains(e.UpdatedBy.Value)),
                    ct
                )
            || await Context
                .Partners.AsNoTracking()
                .AnyAsync(
                    p =>
                        household.Contains(p.CreatedBy)
                        || (p.UpdatedBy != null && household.Contains(p.UpdatedBy.Value)),
                    ct
                )
            || await Context
                .Resources.AsNoTracking()
                .AnyAsync(
                    r =>
                        household.Contains(r.CreatedBy)
                        || (r.UpdatedBy != null && household.Contains(r.UpdatedBy.Value)),
                    ct
                )
            || await Context
                .Files.AsNoTracking()
                .AnyAsync(f => household.Contains(f.UploadedBy), ct);
    }

    private IQueryable<User> QueryWithDetails(bool tracked = false)
    {
        var query = tracked ? Set : Set.AsNoTracking();
        return query.Include(u => u.UserStatusType);
    }
}
