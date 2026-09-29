using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class FakeReadStore : IReadStore
{
    private readonly Dictionary<Type, int> reads = [];

    public List<UserRow> Users { get; } = [];

    public List<UserStatusTypeRow> UserStatusTypes { get; } = [];

    public List<UserTypeRow> UserTypes { get; } = [];

    public List<UserSessionRow> UserSessions { get; } = [];

    public List<EventRow> Events { get; } = [];

    public List<EventRatingRow> EventRatings { get; } = [];

    public List<EventCategoryTypeRow> EventCategoryTypes { get; } = [];

    public List<EventTermsDocumentRow> EventTermsDocuments { get; } = [];

    public List<EventTermsAcceptanceRow> EventTermsAcceptances { get; } = [];

    public List<TermsDocumentRow> TermsDocuments { get; } = [];

    public List<ActivityRow> Activities { get; } = [];

    public List<AssignmentRow> Assignments { get; } = [];

    public List<ActivityRoleTypeRow> ActivityRoleTypes { get; } = [];

    public List<AssignmentStatusTypeRow> AssignmentStatusTypes { get; } = [];

    public List<ActivityModalityTypeRow> ActivityModalityTypes { get; } = [];

    public List<ResourceRow> Resources { get; } = [];

    public List<ResourceTypeRow> ResourceTypes { get; } = [];

    public List<NewsItemRow> News { get; } = [];

    public List<PartnerRow> Partners { get; } = [];

    public List<FileRow> Files { get; } = [];

    IQueryable<UserRow> IReadStore.Users => Read(Users);

    IQueryable<UserStatusTypeRow> IReadStore.UserStatusTypes => Read(UserStatusTypes);

    IQueryable<UserTypeRow> IReadStore.UserTypes => Read(UserTypes);

    IQueryable<UserSessionRow> IReadStore.UserSessions => Read(UserSessions);

    IQueryable<EventRow> IReadStore.Events => Read(Events);

    IQueryable<EventRatingRow> IReadStore.EventRatings => Read(EventRatings);

    IQueryable<EventCategoryTypeRow> IReadStore.EventCategoryTypes => Read(EventCategoryTypes);

    IQueryable<EventTermsDocumentRow> IReadStore.EventTermsDocuments => Read(EventTermsDocuments);

    IQueryable<EventTermsAcceptanceRow> IReadStore.EventTermsAcceptances =>
        Read(EventTermsAcceptances);

    IQueryable<TermsDocumentRow> IReadStore.TermsDocuments => Read(TermsDocuments);

    IQueryable<ActivityRow> IReadStore.Activities => Read(Activities);

    IQueryable<AssignmentRow> IReadStore.Assignments => Read(Assignments);

    IQueryable<ActivityRoleTypeRow> IReadStore.ActivityRoleTypes => Read(ActivityRoleTypes);

    IQueryable<AssignmentStatusTypeRow> IReadStore.AssignmentStatusTypes =>
        Read(AssignmentStatusTypes);

    IQueryable<ActivityModalityTypeRow> IReadStore.ActivityModalityTypes =>
        Read(ActivityModalityTypes);

    IQueryable<ResourceRow> IReadStore.Resources => Read(Resources);

    IQueryable<ResourceTypeRow> IReadStore.ResourceTypes => Read(ResourceTypes);

    IQueryable<NewsItemRow> IReadStore.News => Read(News);

    IQueryable<PartnerRow> IReadStore.Partners => Read(Partners);

    IQueryable<FileRow> IReadStore.Files => Read(Files);

    public int ReadsOf<TRow>()
    {
        return reads.GetValueOrDefault(typeof(TRow));
    }

    private IQueryable<TRow> Read<TRow>(List<TRow> rows)
    {
        reads[typeof(TRow)] = ReadsOf<TRow>() + 1;
        return rows.AsQueryable();
    }
}
