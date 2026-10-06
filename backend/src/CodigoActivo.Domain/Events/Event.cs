using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Event that groups activities people sign up to. It owns its schedule, the categories that tag
/// it and the terms documents participants accept; featuring it is not an edit.
/// </summary>
public class Event : AuditableEntity<EventId>, IFeaturable
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
    public RichText Description { get; private set; } = RichText.Empty;

    /// <summary>
    /// Gets the days the event runs.
    /// </summary>
    public DateRange Calendar => DateRange.FromStored(EventStartsAt, EventEndsAt);

    /// <summary>
    /// Gets when people may sign up to the activities of the event.
    /// </summary>
    public SignupWindow SignupWindow =>
        SignupWindow.FromStored(EarlySignupStartsAt, SignupStartsAt, SignupEndsAt);

    /// <inheritdoc />
    public bool Featured { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public StoredFileId ThumbnailId { get; private set; }

    /// <summary>
    /// Gets the categories that tag the event.
    /// </summary>
    public IReadOnlyCollection<EventCategory> Categories => categories;

    /// <summary>
    /// Gets the terms documents participants accept, in display order.
    /// </summary>
    public IReadOnlyCollection<EventTermsDocument> TermsDocuments => termsDocuments;

    private DateOnly EventStartsAt { get; set; }

    private DateOnly EventEndsAt { get; set; }

    private DateTimeOffset? EarlySignupStartsAt { get; set; }

    private DateTimeOffset SignupStartsAt { get; set; }

    private DateTimeOffset SignupEndsAt { get; set; }

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
        UserId authorId,
        DateTimeOffset now
    )
    {
        var ev = new Event();
        ev.Apply(content, schedule, categorySelection, terms);
        ev.RecordCreation(authorId, now);
        ev.Raise(new EventCreated(ev.Id));
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
        UserId editorId,
        DateTimeOffset now
    )
    {
        var previousThumbnailId = ThumbnailId;
        var previousDescription = Description;
        Apply(content, schedule, categorySelection, terms);
        RecordUpdate(editorId, now);
        Raise(
            new EventUpdated(
                Id,
                ReleasedFiles.Between(
                    previousThumbnailId,
                    ThumbnailId,
                    previousDescription,
                    Description
                )
            )
        );
    }

    /// <summary>
    /// Tells who may sign up to the activities of the event at a given moment.
    /// </summary>
    /// <param name="now">Moment to evaluate.</param>
    /// <returns>The signup phase at that moment.</returns>
    public SignupPhase SignupPhaseAt(DateTimeOffset now)
    {
        return SignupWindow.PhaseAt(now);
    }

    /// <summary>
    /// Tells whether the event is over, so participants may rate it.
    /// </summary>
    /// <param name="today">Current day.</param>
    /// <returns><see langword="true"/> once the last day of the event has passed.</returns>
    public bool HasEndedBy(DateOnly today)
    {
        return Calendar.EndsBefore(today);
    }

    /// <inheritdoc />
    public void Feature()
    {
        ChangeFeatured(true);
    }

    /// <inheritdoc />
    public void Unfeature()
    {
        ChangeFeatured(false);
    }

    /// <summary>
    /// Marks the event as deleted together with its activities, so the files they reference can
    /// be released once they are gone.
    /// </summary>
    /// <param name="activityThumbnailIds">Thumbnails of the activities deleted with the event.</param>
    public void Delete(IReadOnlyCollection<StoredFileId> activityThumbnailIds)
    {
        ArgumentNullException.ThrowIfNull(activityThumbnailIds);
        Raise(
            new EventDeleted(
                Id,
                [.. ReleasedFiles.Of(ThumbnailId, Description).Union(activityThumbnailIds)]
            )
        );
    }

    private void ChangeFeatured(bool featured)
    {
        if (Featured == featured)
        {
            return;
        }

        Featured = featured;
        Raise(new EventFeaturedChanged(Id, featured));
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
        EventStartsAt = schedule.Calendar.Start;
        EventEndsAt = schedule.Calendar.End;
        EarlySignupStartsAt = schedule.SignupWindow.EarlyStartsAt;
        SignupStartsAt = schedule.SignupWindow.StartsAt;
        SignupEndsAt = schedule.SignupWindow.EndsAt;
        SyncCategories(categorySelection.CategoryTypeIds);
        SyncTermsDocuments(terms);
    }

    private void SyncCategories(IReadOnlyCollection<EventCategoryTypeId> categoryTypeIds)
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
