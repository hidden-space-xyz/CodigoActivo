using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Legal;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Conversions;
using CodigoActivo.Infrastructure.DataHistory;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Context;

/// <summary>
/// Represents the Entity Framework session for CodigoActivo data.
/// </summary>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="history">
/// Recorder of the data history, which the container always supplies (`DataHistoryWiringTests`);
/// only the EF Core tools and tests that build the context by hand go without it.
/// </param>
public class CodigoActivoDbContext(
    DbContextOptions<CodigoActivoDbContext> options,
    DataHistoryRecorder? history = null
) : DbContext(options)
{
    /// <summary>
    /// Gets the users value.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Gets the changes of the news consent of the users.
    /// </summary>
    public DbSet<NewsConsentChange> NewsConsentChanges => Set<NewsConsentChange>();

    /// <summary>
    /// Gets the user status types value.
    /// </summary>
    public DbSet<UserStatusEntry> UserStatusTypes => Set<UserStatusEntry>();

    /// <summary>
    /// Gets the user types value.
    /// </summary>
    public DbSet<UserTypeEntry> UserTypes => Set<UserTypeEntry>();

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
    /// Gets the activities value.
    /// </summary>
    public DbSet<Activity> Activities => Set<Activity>();

    /// <summary>
    /// Gets the activity role types value.
    /// </summary>
    public DbSet<ActivityRoleEntry> ActivityRoleTypes => Set<ActivityRoleEntry>();

    /// <summary>
    /// Gets the assignments value.
    /// </summary>
    public DbSet<Assignment> Assignments => Set<Assignment>();

    /// <summary>
    /// Gets the activity role capacities value.
    /// </summary>
    public DbSet<ActivityRoleCapacity> ActivityRoleCapacities => Set<ActivityRoleCapacity>();

    /// <summary>
    /// Gets the assignment status types value.
    /// </summary>
    public DbSet<AssignmentStatusEntry> AssignmentStatusTypes => Set<AssignmentStatusEntry>();

    /// <summary>
    /// Gets the activity modality types value.
    /// </summary>
    public DbSet<ActivityModalityEntry> ActivityModalityTypes => Set<ActivityModalityEntry>();

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
    /// Gets the versions the terms documents replaced.
    /// </summary>
    public DbSet<TermsDocumentVersion> TermsDocumentVersions => Set<TermsDocumentVersion>();

    /// <summary>
    /// Gets the legal policies, with the version in force of each one.
    /// </summary>
    public DbSet<LegalPolicy> LegalPolicies => Set<LegalPolicy>();

    /// <summary>
    /// Gets the versions the legal policies replaced.
    /// </summary>
    public DbSet<LegalPolicyVersion> LegalPolicyVersions => Set<LegalPolicyVersion>();

    /// <summary>
    /// Gets the event terms documents value.
    /// </summary>
    public DbSet<EventTermsDocument> EventTermsDocuments => Set<EventTermsDocument>();

    /// <summary>
    /// Gets the event terms acceptances value.
    /// </summary>
    public DbSet<EventTermsAcceptance> EventTermsAcceptances => Set<EventTermsAcceptance>();

    /// <summary>
    /// Gets every change of the terms decisions.
    /// </summary>
    public DbSet<TermsDecisionChange> TermsDecisionChanges => Set<TermsDecisionChange>();

    /// <summary>
    /// Gets the resources value.
    /// </summary>
    public DbSet<Resource> Resources => Set<Resource>();

    /// <summary>
    /// Gets the resource types value.
    /// </summary>
    public DbSet<ResourceTypeEntry> ResourceTypes => Set<ResourceTypeEntry>();

    /// <summary>
    /// Gets the news value.
    /// </summary>
    public DbSet<NewsItem> News => Set<NewsItem>();

    /// <summary>
    /// Gets the files value.
    /// </summary>
    public DbSet<StoredFile> Files => Set<StoredFile>();

    /// <summary>
    /// Gets the partners value.
    /// </summary>
    public DbSet<Partner> Partners => Set<Partner>();

    /// <summary>
    /// Gets the email outbox messages value.
    /// </summary>
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();

    /// <summary>
    /// Gets the email outbox contents value.
    /// </summary>
    public DbSet<EmailOutboxContent> EmailOutboxContents => Set<EmailOutboxContent>();

    /// <summary>
    /// Gets the email outbox content parts value.
    /// </summary>
    public DbSet<EmailOutboxContentPart> EmailOutboxContentParts => Set<EmailOutboxContentPart>();

    /// <summary>
    /// Gets the disposable email domains value.
    /// </summary>
    public DbSet<DisposableEmailDomain> DisposableEmailDomains => Set<DisposableEmailDomain>();

    /// <summary>
    /// Gets the encrypted previous states of the historized rows that were changed or deleted.
    /// </summary>
    public DbSet<DataHistoryEntry> DataHistory => Set<DataHistoryEntry>();

    /// <summary>
    /// Gets the consultations of the blocked data.
    /// </summary>
    public DbSet<BlockedDataConsultation> BlockedDataConsultations =>
        Set<BlockedDataConsultation>();

    /// <summary>
    /// Adds <see cref="InitialAdministratorGuard"/> to every instance, whatever registered the
    /// options, so no commit can delete the initial administrator, and the data history recorder
    /// of the scope, so no commit of a historized row goes without its history.
    /// </summary>
    /// <param name="optionsBuilder">Builder of the options of this instance.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(InitialAdministratorGuard.Instance);
        if (history is not null)
        {
            optionsBuilder.AddInterceptors(history);
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        base.ConfigureConventions(configurationBuilder);
        DomainValueConventions.Apply(configurationBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CodigoActivoDbContext).Assembly);
    }
}
