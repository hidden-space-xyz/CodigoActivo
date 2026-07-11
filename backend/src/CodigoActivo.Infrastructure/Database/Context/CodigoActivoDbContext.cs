using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Context;

public class CodigoActivoDbContext(DbContextOptions<CodigoActivoDbContext> options)
    : DbContext(options),
        IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserStatusType> UserStatusTypes => Set<UserStatusType>();
    public DbSet<UserType> UserTypes => Set<UserType>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityRoleType> ActivityRoleTypes => Set<ActivityRoleType>();

    public DbSet<ActivityUserRoleAssignment> ActivityUserRoleAssignments =>
        Set<ActivityUserRoleAssignment>();

    public DbSet<ActivityRoleCapacity> ActivityRoleCapacities => Set<ActivityRoleCapacity>();

    public DbSet<AssignmentStatusType> AssignmentStatusTypes => Set<AssignmentStatusType>();
    public DbSet<ActivityModalityType> ActivityModalityTypes => Set<ActivityModalityType>();

    public DbSet<EventCategoryType> EventCategoryTypes => Set<EventCategoryType>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<FileEntity> Files => Set<FileEntity>();
    public DbSet<Partner> Partners => Set<Partner>();

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        return base.SaveChangesAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodigoActivoDbContext).Assembly);
    }
}
