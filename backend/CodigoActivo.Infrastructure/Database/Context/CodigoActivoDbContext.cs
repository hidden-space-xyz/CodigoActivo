using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Context;

public class CodigoActivoDbContext(DbContextOptions<CodigoActivoDbContext> options) : DbContext(options),
        IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserStatusType> UserStatusTypes => Set<UserStatusType>();
    public DbSet<UserType> UserTypes => Set<UserType>();
    public DbSet<UserTypeAssignment> UserTypeAssignments => Set<UserTypeAssignment>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityRoleType> ActivityRoleTypes => Set<ActivityRoleType>();
    public DbSet<ActivityAllowedRoleType> ActivityAllowedRoleTypes =>
        Set<ActivityAllowedRoleType>();
    public DbSet<ActivityUserRoleAssignment> ActivityUserRoleAssignments =>
        Set<ActivityUserRoleAssignment>();
    public DbSet<AssignmentStatusType> AssignmentStatusTypes => Set<AssignmentStatusType>();

    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<FileEntity> Files => Set<FileEntity>();
    public DbSet<Partner> Partners => Set<Partner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodigoActivoDbContext).Assembly);
    }

    Task<int> IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        return base.SaveChangesAsync(ct);
    }
}
