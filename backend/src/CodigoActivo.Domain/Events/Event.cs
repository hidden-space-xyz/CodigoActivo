using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Event that groups activities people sign up to. It owns its schedule, the categories that tag
/// it and the terms documents participants accept; featuring it is not an edit.
/// </summary>
public class Event : AuditableEntity, IAggregateRoot, IFeaturable
{
    private readonly List<EventCategory> categories = [];
    private readonly List<EventTermsDocument> termsDocuments = [];

    private Event() { }

    /// <summary>
    /// Gets the title displayed to users.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the supporting subtitle displayed to users.
    /// </summary>
    public string Subtitle { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich-text description.
    /// </summary>
    public string Description { get; private set; } = "{}";

    /// <summary>
    /// Gets the first day of the event.
    /// </summary>
    public DateOnly EventStartsAt { get; private set; }

    /// <summary>
    /// Gets the last day of the event.
    /// </summary>
    public DateOnly EventEndsAt { get; private set; }

    /// <summary>
    /// Gets when the early signup opens, if there is one.
    /// </summary>
    public DateTimeOffset? EarlySignupStartsAt { get; private set; }

    /// <summary>
    /// Gets when the signup opens.
    /// </summary>
    public DateTimeOffset SignupStartsAt { get; private set; }

    /// <summary>
    /// Gets when the signup closes.
    /// </summary>
    public DateTimeOffset SignupEndsAt { get; private set; }

    /// <inheritdoc />
    public bool Featured { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public Guid ThumbnailId { get; private set; }

    /// <summary>
    /// Gets the categories that tag the event.
    /// </summary>
    public IReadOnlyCollection<EventCategory> Categories => categories;

    /// <summary>
    /// Gets the terms documents participants accept, in display order.
    /// </summary>
    public IReadOnlyCollection<EventTermsDocument> TermsDocuments => termsDocuments;

    /// <summary>
    /// Creates an event.
    /// </summary>
    /// <param name="content">Titles, description and thumbnail.</param>
    /// <param name="schedule">Days and signup window.</param>
    /// <param name="categorySelection">Categories that tag the event.</param>
    /// <param name="terms">Terms documents in display order.</param>
    /// <param name="authorId">Identifier of the user who creates it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new event.</returns>
    public static Event Create(
        EventContent content,
        EventSchedule schedule,
        EventCategorySelection categorySelection,
        EventTermsLinks terms,
        Guid authorId,
        DateTimeOffset now
    )
    {
        var ev = new Event();
        ev.Apply(content, schedule, categorySelection, terms);
        ev.RecordCreation(authorId, now);
        return ev;
    }

    /// <summary>
    /// Replaces the event, keeping the categories and terms links that are still wanted.
    /// </summary>
    /// <param name="content">New titles, description and thumbnail.</param>
    /// <param name="schedule">New days and signup window.</param>
    /// <param name="categorySelection">Categories that tag the event from now on.</param>
    /// <param name="terms">Terms documents from now on, in display order.</param>
    /// <param name="editorId">Identifier of the user who edits it.</param>
    /// <param name="now">Current time.</param>
    public void Update(
        EventContent content,
        EventSchedule schedule,
        EventCategorySelection categorySelection,
        EventTermsLinks terms,
        Guid editorId,
        DateTimeOffset now
    )
    {
        Apply(content, schedule, categorySelection, terms);
        RecordUpdate(editorId, now);
    }

    /// <summary>
    /// Tells who may sign up to the activities of the event at a given moment.
    /// </summary>
    /// <param name="now">Moment to evaluate.</param>
    /// <returns>The signup phase at that moment.</returns>
    public SignupPhase SignupPhaseAt(DateTimeOffset now)
    {
        return EventTimeline.SignupPhaseAt(EarlySignupStartsAt, SignupStartsAt, SignupEndsAt, now);
    }

    /// <summary>
    /// Tells whether the event is over, so participants may rate it.
    /// </summary>
    /// <param name="today">Current day.</param>
    /// <returns><see langword="true"/> once the last day of the event has passed.</returns>
    public bool HasEndedBy(DateOnly today)
    {
        return EventEndsAt < today;
    }

    /// <inheritdoc />
    public void Feature()
    {
        Featured = true;
    }

    /// <inheritdoc />
    public void Unfeature()
    {
        Featured = false;
    }

    private void Apply(
        EventContent content,
        EventSchedule schedule,
        EventCategorySelection categorySelection,
        EventTermsLinks terms
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(categorySelection);
        ArgumentNullException.ThrowIfNull(terms);

        Title = content.Title.Trim();
        Subtitle = content.Subtitle.Trim();
        Description = content.Description;
        ThumbnailId = content.ThumbnailId;
        EventStartsAt = schedule.EventStartsAt;
        EventEndsAt = schedule.EventEndsAt;
        EarlySignupStartsAt = schedule.EarlySignupStartsAt;
        SignupStartsAt = schedule.SignupStartsAt;
        SignupEndsAt = schedule.SignupEndsAt;
        SyncCategories(categorySelection.CategoryTypeIds);
        SyncTermsDocuments(terms);
    }

    private void SyncCategories(IReadOnlyCollection<Guid> categoryTypeIds)
    {
        var desired = categoryTypeIds.ToHashSet();
        categories.RemoveAll(category => !desired.Contains(category.EventCategoryTypeId));

        var current = categories.Select(category => category.EventCategoryTypeId).ToHashSet();
        foreach (var categoryTypeId in desired.Except(current))
        {
            categories.Add(new EventCategory(Id, categoryTypeId));
        }
    }

    private void SyncTermsDocuments(EventTermsLinks terms)
    {
        var desiredIds = terms.Items.Select(link => link.TermsDocumentId).ToHashSet();
        termsDocuments.RemoveAll(document => !desiredIds.Contains(document.TermsDocumentId));

        var current = termsDocuments.ToDictionary(document => document.TermsDocumentId);
        var order = 0;
        foreach (var link in terms.Items)
        {
            if (current.TryGetValue(link.TermsDocumentId, out var existing))
            {
                existing.Place(link.Required, order);
            }
            else
            {
                termsDocuments.Add(
                    new EventTermsDocument(Id, link.TermsDocumentId, link.Required, order)
                );
            }

            order++;
        }
    }
}
