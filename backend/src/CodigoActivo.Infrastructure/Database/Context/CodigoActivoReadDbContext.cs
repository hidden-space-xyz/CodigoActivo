using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Context;

/// <summary>
/// Untracked Entity Framework session behind the read side: it maps the read models onto the
/// tables owned by <see cref="CodigoActivoDbContext"/> and never takes part in migrations.
/// </summary>
/// <param name="options">Configuration values used by the component.</param>
public sealed class CodigoActivoReadDbContext(DbContextOptions<CodigoActivoReadDbContext> options)
    : DbContext(options),
        IReadStore
{
    /// <inheritdoc />
    public IQueryable<UserRow> Users => Set<UserRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<UserStatusTypeRow> UserStatusTypes => Set<UserStatusTypeRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<UserTypeRow> UserTypes => Set<UserTypeRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<UserSessionRow> UserSessions => Set<UserSessionRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<EventRow> Events => Set<EventRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<EventRatingRow> EventRatings => Set<EventRatingRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<EventCategoryTypeRow> EventCategoryTypes =>
        Set<EventCategoryTypeRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<EventTermsDocumentRow> EventTermsDocuments =>
        Set<EventTermsDocumentRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<EventTermsAcceptanceRow> EventTermsAcceptances =>
        Set<EventTermsAcceptanceRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<TermsDocumentRow> TermsDocuments => Set<TermsDocumentRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<ActivityRow> Activities => Set<ActivityRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<AssignmentRow> Assignments => Set<AssignmentRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<ActivityRoleTypeRow> ActivityRoleTypes =>
        Set<ActivityRoleTypeRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<AssignmentStatusTypeRow> AssignmentStatusTypes =>
        Set<AssignmentStatusTypeRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<ActivityModalityTypeRow> ActivityModalityTypes =>
        Set<ActivityModalityTypeRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<ResourceRow> Resources => Set<ResourceRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<ResourceTypeRow> ResourceTypes => Set<ResourceTypeRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<NewsItemRow> News => Set<NewsItemRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<PartnerRow> Partners => Set<PartnerRow>().AsNoTracking();

    /// <inheritdoc />
    public IQueryable<FileRow> Files => Set<FileRow>().AsNoTracking();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureEvents(modelBuilder);
        ConfigureActivities(modelBuilder);
        ConfigureContent(modelBuilder);
    }

    private static EntityTypeBuilder<TRow> Table<TRow>(ModelBuilder modelBuilder, string name)
        where TRow : class
    {
        return modelBuilder.Entity<TRow>().ToTable(name, table => table.ExcludeFromMigrations());
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var users = Table<UserRow>(modelBuilder, "users");
        users.HasKey(u => u.Id);
        users.Property(u => u.Gender).HasConversion<string>();
        users.Property(u => u.TwoFactorMethod).HasConversion<string>();
        users.HasOne(u => u.Parent).WithMany(u => u.Children).HasForeignKey(u => u.ParentId);
        users.HasOne(u => u.UserStatusType).WithMany().HasForeignKey(u => u.UserStatusTypeId);
        users.HasOne(u => u.UserType).WithMany().HasForeignKey(u => u.UserTypeId);

        Table<UserStatusTypeRow>(modelBuilder, "user_status_types").HasKey(s => s.Id);
        Table<UserTypeRow>(modelBuilder, "user_types").HasKey(t => t.Id);
        Table<UserSessionRow>(modelBuilder, "user_sessions").HasKey(s => s.Id);
    }

    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        var events = Table<EventRow>(modelBuilder, "events");
        events.HasKey(e => e.Id);
        events.Property(e => e.Description).HasColumnType("jsonb");

        var ratings = Table<EventRatingRow>(modelBuilder, "event_ratings");
        ratings.HasKey(r => r.Id);
        ratings.HasOne(r => r.Event).WithMany(e => e.Ratings).HasForeignKey(r => r.EventId);

        Table<EventCategoryTypeRow>(modelBuilder, "event_category_types").HasKey(t => t.Id);

        var categories = Table<EventCategoryRow>(modelBuilder, "event_categories");
        categories.HasKey(c => new { c.EventId, c.EventCategoryTypeId });
        categories.HasOne(c => c.Event).WithMany(e => e.Categories).HasForeignKey(c => c.EventId);
        categories
            .HasOne(c => c.EventCategoryType)
            .WithMany(t => t.Events)
            .HasForeignKey(c => c.EventCategoryTypeId);

        var termsDocuments = Table<TermsDocumentRow>(modelBuilder, "terms_documents");
        termsDocuments.HasKey(d => d.Id);
        termsDocuments.Property(d => d.Description).HasColumnType("jsonb");

        var links = Table<EventTermsDocumentRow>(modelBuilder, "event_terms_documents");
        links.HasKey(l => new { l.EventId, l.TermsDocumentId });
        links.HasOne(l => l.Event).WithMany(e => e.TermsDocuments).HasForeignKey(l => l.EventId);
        links.HasOne(l => l.TermsDocument).WithMany().HasForeignKey(l => l.TermsDocumentId);

        var acceptances = Table<EventTermsAcceptanceRow>(modelBuilder, "event_terms_acceptances");
        acceptances.HasKey(a => new
        {
            a.EventId,
            a.UserId,
            a.TermsDocumentId,
        });
        acceptances.HasOne(a => a.Event).WithMany().HasForeignKey(a => a.EventId);
        acceptances.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
    }

    private static void ConfigureActivities(ModelBuilder modelBuilder)
    {
        var activities = Table<ActivityRow>(modelBuilder, "activities");
        activities.HasKey(a => a.Id);
        activities.HasOne(a => a.Event).WithMany(e => e.Activities).HasForeignKey(a => a.EventId);
        activities
            .HasOne(a => a.ActivityModalityType)
            .WithMany()
            .HasForeignKey(a => a.ActivityModalityTypeId);

        var assignments = Table<AssignmentRow>(modelBuilder, "assignments");
        assignments.HasKey(a => new
        {
            a.UserId,
            a.ActivityId,
            a.ActivityRoleTypeId,
        });
        assignments.HasOne(a => a.User).WithMany(u => u.Assignments).HasForeignKey(a => a.UserId);
        assignments
            .HasOne(a => a.Activity)
            .WithMany(a => a.Assignments)
            .HasForeignKey(a => a.ActivityId);
        assignments
            .HasOne(a => a.ActivityRoleType)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.ActivityRoleTypeId);
        assignments
            .HasOne(a => a.AssignmentStatus)
            .WithMany()
            .HasForeignKey(a => a.AssignmentStatusId);

        var capacities = Table<RoleCapacityRow>(modelBuilder, "activity_role_capacities");
        capacities.HasKey(c => new { c.ActivityId, c.ActivityRoleTypeId });
        capacities
            .HasOne(c => c.Activity)
            .WithMany(a => a.RoleCapacities)
            .HasForeignKey(c => c.ActivityId);
        capacities
            .HasOne(c => c.ActivityRoleType)
            .WithMany()
            .HasForeignKey(c => c.ActivityRoleTypeId);

        Table<ActivityRoleTypeRow>(modelBuilder, "activity_role_types").HasKey(t => t.Id);
        Table<AssignmentStatusTypeRow>(modelBuilder, "assignment_status_types").HasKey(s => s.Id);
        Table<ActivityModalityTypeRow>(modelBuilder, "activity_modality_types").HasKey(m => m.Id);
    }

    private static void ConfigureContent(ModelBuilder modelBuilder)
    {
        var resources = Table<ResourceRow>(modelBuilder, "resources");
        resources.HasKey(r => r.Id);
        resources.Property(r => r.Description).HasColumnType("jsonb");
        resources.HasOne(r => r.ResourceType).WithMany().HasForeignKey(r => r.ResourceTypeId);

        Table<ResourceTypeRow>(modelBuilder, "resource_types").HasKey(t => t.Id);

        var news = Table<NewsItemRow>(modelBuilder, "news");
        news.HasKey(n => n.Id);
        news.Property(n => n.Description).HasColumnType("jsonb");

        Table<PartnerRow>(modelBuilder, "partners").HasKey(p => p.Id);
        Table<FileRow>(modelBuilder, "files").HasKey(f => f.Id);
    }
}
