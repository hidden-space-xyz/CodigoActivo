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
/// Write-side Entity Framework session: it tracks the aggregates the repositories load, so the unit
/// of work saves their changes, and owns the model the migrations are built from.
/// </summary>
/// <param name="options">Options that point the context at the write database, as set by
/// <see cref="PostgresContexts.UseWriteDatabase"/>.</param>
/// <param name="history">
/// Recorder of the data history, which the container always supplies (<c>DataHistoryWiringTests</c>);
/// only the EF Core tools and tests that build the context by hand go without it.
/// </param>
public class CodigoActivoDbContext(
    DbContextOptions<CodigoActivoDbContext> options,
    DataHistoryRecorder? history = null
) : DbContext(options)
{
    /// <summary>
    /// Gets the user accounts, adults and dependents alike.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Gets the changes of the news consent of the users.
    /// </summary>
    public DbSet<NewsConsentChange> NewsConsentChanges => Set<NewsConsentChange>();

    /// <summary>
    /// Gets the reference rows of the user statuses.
    /// </summary>
    public DbSet<UserStatusEntry> UserStatusTypes => Set<UserStatusEntry>();

    /// <summary>
    /// Gets the reference rows of the user types.
    /// </summary>
    public DbSet<UserTypeEntry> UserTypes => Set<UserTypeEntry>();

    /// <summary>
    /// Gets the sessions opened by the accounts.
    /// </summary>
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    /// <summary>
    /// Gets the events.
    /// </summary>
    public DbSet<Event> Events => Set<Event>();

    /// <summary>
    /// Gets the ratings people gave to events.
    /// </summary>
    public DbSet<EventRating> EventRatings => Set<EventRating>();

    /// <summary>
    /// Gets the activities of the events.
    /// </summary>
    public DbSet<Activity> Activities => Set<Activity>();

    /// <summary>
    /// Gets the reference rows of the activity roles.
    /// </summary>
    public DbSet<ActivityRoleEntry> ActivityRoleTypes => Set<ActivityRoleEntry>();

    /// <summary>
    /// Gets the signups of people to activities.
    /// </summary>
    public DbSet<Assignment> Assignments => Set<Assignment>();

    /// <summary>
    /// Gets the number of people each activity wants in each role.
    /// </summary>
    public DbSet<ActivityRoleCapacity> ActivityRoleCapacities => Set<ActivityRoleCapacity>();

    /// <summary>
    /// Gets the reference rows of the signup statuses.
    /// </summary>
    public DbSet<AssignmentStatusEntry> AssignmentStatusTypes => Set<AssignmentStatusEntry>();

    /// <summary>
    /// Gets the reference rows of the activity modalities.
    /// </summary>
    public DbSet<ActivityModalityEntry> ActivityModalityTypes => Set<ActivityModalityEntry>();

    /// <summary>
    /// Gets the categories administrators define for tagging events.
    /// </summary>
    public DbSet<EventCategoryType> EventCategoryTypes => Set<EventCategoryType>();

    /// <summary>
    /// Gets the links between events and their categories.
    /// </summary>
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    /// <summary>
    /// Gets the terms documents, with the version in force of each one.
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
    /// Gets the links between events and the terms documents they require.
    /// </summary>
    public DbSet<EventTermsDocument> EventTermsDocuments => Set<EventTermsDocument>();

    /// <summary>
    /// Gets the current terms decision of each person, event and terms document.
    /// </summary>
    public DbSet<EventTermsAcceptance> EventTermsAcceptances => Set<EventTermsAcceptance>();

    /// <summary>
    /// Gets every change of the terms decisions.
    /// </summary>
    public DbSet<TermsDecisionChange> TermsDecisionChanges => Set<TermsDecisionChange>();

    /// <summary>
    /// Gets the resources.
    /// </summary>
    public DbSet<Resource> Resources => Set<Resource>();

    /// <summary>
    /// Gets the reference rows of the resource types.
    /// </summary>
    public DbSet<ResourceTypeEntry> ResourceTypes => Set<ResourceTypeEntry>();

    /// <summary>
    /// Gets the news items.
    /// </summary>
    public DbSet<NewsItem> News => Set<NewsItem>();

    /// <summary>
    /// Gets the uploaded files, whose content lives in the file storage.
    /// </summary>
    public DbSet<StoredFile> Files => Set<StoredFile>();

    /// <summary>
    /// Gets the partners.
    /// </summary>
    public DbSet<Partner> Partners => Set<Partner>();

    /// <summary>
    /// Gets the pending deliveries of the email outbox; a row is deleted once it is delivered or its
    /// attempts are spent.
    /// </summary>
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();

    /// <summary>
    /// Gets the protected subject and bodies of the outbox emails, shared by their pending deliveries.
    /// </summary>
    public DbSet<EmailOutboxContent> EmailOutboxContents => Set<EmailOutboxContent>();

    /// <summary>
    /// Gets the attachments and inline images of the outbox emails.
    /// </summary>
    public DbSet<EmailOutboxContentPart> EmailOutboxContentParts => Set<EmailOutboxContentPart>();

    /// <summary>
    /// Gets the stored disposable email domain list.
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
