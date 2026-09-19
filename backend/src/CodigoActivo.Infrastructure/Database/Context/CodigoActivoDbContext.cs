using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodigoActivo.Infrastructure.Database.Context;

/// <summary>
/// Represents the Entity Framework session for CodigoActivo data.
/// </summary>
/// <param name="options">Configuration values used by the component.</param>
public class CodigoActivoDbContext(DbContextOptions<CodigoActivoDbContext> options)
    : DbContext(options),
        IUnitOfWork
{
    /// <summary>
    /// Gets the users value.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Gets the user status types value.
    /// </summary>
    public DbSet<UserStatusType> UserStatusTypes => Set<UserStatusType>();

    /// <summary>
    /// Gets the user types value.
    /// </summary>
    public DbSet<UserType> UserTypes => Set<UserType>();

    /// <summary>
    /// Gets the user sessions value.
    /// </summary>
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    /// <summary>
    /// Gets the events value.
    /// </summary>
    public DbSet<Event> Events => Set<Event>();

    /// <summary>
    /// Gets the event ratings value.
    /// </summary>
    public DbSet<EventRating> EventRatings => Set<EventRating>();

    /// <summary>
    /// Gets the event rating submissions value.
    /// </summary>
    public DbSet<EventRatingSubmission> EventRatingSubmissions => Set<EventRatingSubmission>();

    /// <summary>
    /// Gets the activities value.
    /// </summary>
    public DbSet<Activity> Activities => Set<Activity>();

    /// <summary>
    /// Gets the activity role types value.
    /// </summary>
    public DbSet<ActivityRoleType> ActivityRoleTypes => Set<ActivityRoleType>();

    /// <summary>
    /// Gets the activity user role assignments value.
    /// </summary>
    public DbSet<ActivityUserRoleAssignment> ActivityUserRoleAssignments =>
        Set<ActivityUserRoleAssignment>();

    /// <summary>
    /// Gets the activity role capacities value.
    /// </summary>
    public DbSet<ActivityRoleCapacity> ActivityRoleCapacities => Set<ActivityRoleCapacity>();

    /// <summary>
    /// Gets the assignment status types value.
    /// </summary>
    public DbSet<AssignmentStatusType> AssignmentStatusTypes => Set<AssignmentStatusType>();

    /// <summary>
    /// Gets the activity modality types value.
    /// </summary>
    public DbSet<ActivityModalityType> ActivityModalityTypes => Set<ActivityModalityType>();

    /// <summary>
    /// Gets the event category types value.
    /// </summary>
    public DbSet<EventCategoryType> EventCategoryTypes => Set<EventCategoryType>();

    /// <summary>
    /// Gets the event categories value.
    /// </summary>
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    /// <summary>
    /// Gets the terms documents value.
    /// </summary>
    public DbSet<TermsDocument> TermsDocuments => Set<TermsDocument>();

    /// <summary>
    /// Gets the event terms documents value.
    /// </summary>
    public DbSet<EventTermsDocument> EventTermsDocuments => Set<EventTermsDocument>();

    /// <summary>
    /// Gets the event terms acceptances value.
    /// </summary>
    public DbSet<EventTermsAcceptance> EventTermsAcceptances => Set<EventTermsAcceptance>();

    /// <summary>
    /// Gets the resources value.
    /// </summary>
    public DbSet<Resource> Resources => Set<Resource>();

    /// <summary>
    /// Gets the resource types value.
    /// </summary>
    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();

    /// <summary>
    /// Gets the announcements value.
    /// </summary>
    public DbSet<Announcement> Announcements => Set<Announcement>();

    /// <summary>
    /// Gets the files value.
    /// </summary>
    public DbSet<FileEntity> Files => Set<FileEntity>();

    /// <summary>
    /// Gets the partners value.
    /// </summary>
    public DbSet<Partner> Partners => Set<Partner>();

    async Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await base.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException
                    is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            )
        {
            throw new UniqueConstraintViolationException(postgres.ConstraintName, ex);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodigoActivoDbContext).Assembly);
    }
}
