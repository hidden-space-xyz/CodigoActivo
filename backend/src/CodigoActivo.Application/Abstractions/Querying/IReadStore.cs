using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.Application.Abstractions.Querying;

/// <summary>
/// Read side of the application: untracked sources the queries compose and project to their
/// response shapes, independent of the aggregates and repositories of the write side.
/// </summary>
public interface IReadStore
{
    /// <summary>Gets the stored accounts.</summary>
    public IQueryable<UserRow> Users { get; }

    /// <summary>Gets the account statuses.</summary>
    public IQueryable<UserStatusTypeRow> UserStatusTypes { get; }

    /// <summary>Gets the membership types.</summary>
    public IQueryable<UserTypeRow> UserTypes { get; }

    /// <summary>Gets the stored sessions.</summary>
    public IQueryable<UserSessionRow> UserSessions { get; }

    /// <summary>Gets the stored events.</summary>
    public IQueryable<EventRow> Events { get; }

    /// <summary>Gets the anonymous event ratings.</summary>
    public IQueryable<EventRatingRow> EventRatings { get; }

    /// <summary>Gets the event category types.</summary>
    public IQueryable<EventCategoryTypeRow> EventCategoryTypes { get; }

    /// <summary>Gets the terms documents linked to the events.</summary>
    public IQueryable<EventTermsDocumentRow> EventTermsDocuments { get; }

    /// <summary>Gets the recorded terms decisions.</summary>
    public IQueryable<EventTermsAcceptanceRow> EventTermsAcceptances { get; }

    /// <summary>Gets the terms documents.</summary>
    public IQueryable<TermsDocumentRow> TermsDocuments { get; }

    /// <summary>Gets the stored activities.</summary>
    public IQueryable<ActivityRow> Activities { get; }

    /// <summary>Gets the activity assignments.</summary>
    public IQueryable<AssignmentRow> Assignments { get; }

    /// <summary>Gets the activity roles.</summary>
    public IQueryable<ActivityRoleTypeRow> ActivityRoleTypes { get; }

    /// <summary>Gets the assignment statuses.</summary>
    public IQueryable<AssignmentStatusTypeRow> AssignmentStatusTypes { get; }

    /// <summary>Gets the activity modalities.</summary>
    public IQueryable<ActivityModalityTypeRow> ActivityModalityTypes { get; }

    /// <summary>Gets the stored resources.</summary>
    public IQueryable<ResourceRow> Resources { get; }

    /// <summary>Gets the resource types.</summary>
    public IQueryable<ResourceTypeRow> ResourceTypes { get; }

    /// <summary>Gets the stored news items.</summary>
    public IQueryable<NewsItemRow> News { get; }

    /// <summary>Gets the stored partners.</summary>
    public IQueryable<PartnerRow> Partners { get; }

    /// <summary>Gets the stored file metadata.</summary>
    public IQueryable<FileRow> Files { get; }
}
