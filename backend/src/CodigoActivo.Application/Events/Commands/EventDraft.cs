using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Everything an event is made of when it is created or replaced.
/// </summary>
/// <param name="Title">Title.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">Body as rich text JSON.</param>
/// <param name="EventStartsAt">First day of the event.</param>
/// <param name="EventEndsAt">Last day of the event.</param>
/// <param name="EarlySignupStartsAt">Start of the early signup for members, if any.</param>
/// <param name="SignupStartsAt">Start of the signup.</param>
/// <param name="SignupEndsAt">End of the signup.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail file.</param>
/// <param name="CategoryTypeIds">Identifiers of the category types the event belongs to.</param>
/// <param name="TermsDocuments">Terms documents linked to the event, in display order.</param>
public sealed record EventDraft(
    [property: Required, MaxLength(200), NotBlank] string Title,
    [property: Required, MaxLength(300), NotBlank] string Subtitle,
    [property: RichText, MaxLength(RichTextAllowlist.MaxJsonLength)] string Description,
    DateOnly? EventStartsAt,
    DateOnly? EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    DateTimeOffset? SignupStartsAt,
    DateTimeOffset? SignupEndsAt,
    StoredFileId ThumbnailId,
    [property: MaxLength(50)] IReadOnlyList<EventCategoryTypeId> CategoryTypeIds,
    [property: MaxLength(50)] IReadOnlyList<EventTermsLink> TermsDocuments
)
{
    /// <summary>
    /// Gets the schedule the event is created or replaced with.
    /// </summary>
    /// <returns>The schedule, or the rule its dates break.</returns>
    public Result<EventSchedule> ToSchedule()
    {
        return EventSchedule.Create(
            EventStartsAt,
            EventEndsAt,
            EarlySignupStartsAt,
            SignupStartsAt,
            SignupEndsAt
        );
    }

    /// <summary>
    /// Gets the content the event is created or replaced with.
    /// </summary>
    /// <param name="description">Description read from <see cref="Description"/>.</param>
    /// <returns>The content.</returns>
    public EventContent ToContent(RichText description)
    {
        return new EventContent(Title, Subtitle, description, ThumbnailId);
    }
}
