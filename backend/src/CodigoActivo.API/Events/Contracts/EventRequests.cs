using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Events.Contracts;

/// <summary>
/// Contains the client-supplied data used to create an event.
/// </summary>
/// <param name="Title">Title of the event.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">Body as rich text JSON within the rich text allowlist.</param>
/// <param name="EventStartsAt">First day of the event.</param>
/// <param name="EventEndsAt">Last day of the event, not before the first.</param>
/// <param name="EarlySignupStartsAt">
/// Start of the early signup, before the regular one; null when there is none.
/// </param>
/// <param name="SignupStartsAt">Start of the signup.</param>
/// <param name="SignupEndsAt">
/// End of the signup, after its start and no later than the last day of the event.
/// </param>
/// <param name="ThumbnailId">Identifier of the uploaded thumbnail file, which must exist.</param>
/// <param name="CategoryTypeIds">
/// Identifiers of the category types of the event; at least one, each existing.
/// </param>
/// <param name="TermsDocuments">
/// Terms documents linked to the event, in display order, each once; a consent cannot be required.
/// </param>
public record CreateEventRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(RichTextAllowlist.MaxJsonLength)] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    [MaxLength(50)] IReadOnlyList<Guid>? CategoryTypeIds,
    [MaxLength(50)] IReadOnlyList<EventTermsDocumentRequest>? TermsDocuments
)
{
    /// <summary>
    /// Builds the command that creates the event.
    /// </summary>
    /// <returns>The command.</returns>
    public CreateEventCommand ToCommand()
    {
        return new CreateEventCommand(
            EventRequestMapping.Draft(
                Title,
                Subtitle,
                Description,
                EventStartsAt,
                EventEndsAt,
                EarlySignupStartsAt,
                SignupStartsAt,
                SignupEndsAt,
                ThumbnailId,
                CategoryTypeIds,
                TermsDocuments
            )
        );
    }
}

/// <summary>
/// Contains the client-supplied data that replaces every editable field of an event.
/// </summary>
/// <param name="Title">Title of the event.</param>
/// <param name="Subtitle">Line shown below the title.</param>
/// <param name="Description">Body as rich text JSON within the rich text allowlist.</param>
/// <param name="EventStartsAt">First day of the event; every activity must stay within the new days.</param>
/// <param name="EventEndsAt">Last day of the event, not before the first.</param>
/// <param name="EarlySignupStartsAt">
/// Start of the early signup, before the regular one; null when there is none.
/// </param>
/// <param name="SignupStartsAt">Start of the signup.</param>
/// <param name="SignupEndsAt">
/// End of the signup, after its start and no later than the last day of the event.
/// </param>
/// <param name="ThumbnailId">Identifier of the uploaded thumbnail file, which must exist.</param>
/// <param name="CategoryTypeIds">
/// Identifiers of the category types of the event; at least one, each existing.
/// </param>
/// <param name="TermsDocuments">
/// Terms documents linked to the event, in display order, each once; a consent cannot be required.
/// </param>
public record UpdateEventRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(RichTextAllowlist.MaxJsonLength)] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    [MaxLength(50)] IReadOnlyList<Guid>? CategoryTypeIds,
    [MaxLength(50)] IReadOnlyList<EventTermsDocumentRequest>? TermsDocuments
)
{
    /// <summary>
    /// Builds the command that updates the event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <returns>The command.</returns>
    public UpdateEventCommand ToCommand(EventId eventId)
    {
        return new UpdateEventCommand(
            eventId,
            EventRequestMapping.Draft(
                Title,
                Subtitle,
                Description,
                EventStartsAt,
                EventEndsAt,
                EarlySignupStartsAt,
                SignupStartsAt,
                SignupEndsAt,
                ThumbnailId,
                CategoryTypeIds,
                TermsDocuments
            )
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to link a terms document to an event.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Required">
/// Whether accepting the document is mandatory to complete the signup; never for a consent.
/// </param>
public record EventTermsDocumentRequest([Required] Guid TermsDocumentId, bool Required = false);

internal static class EventRequestMapping
{
    public static EventDraft Draft(
        string title,
        string subtitle,
        string description,
        DateOnly? eventStartsAt,
        DateOnly? eventEndsAt,
        DateTimeOffset? earlySignupStartsAt,
        DateTimeOffset? signupStartsAt,
        DateTimeOffset? signupEndsAt,
        Guid thumbnailId,
        IReadOnlyList<Guid>? categoryTypeIds,
        IReadOnlyList<EventTermsDocumentRequest>? termsDocuments
    )
    {
        return new EventDraft(
            title,
            subtitle,
            description,
            eventStartsAt,
            eventEndsAt,
            earlySignupStartsAt,
            signupStartsAt,
            signupEndsAt,
            StoredFileId.From(thumbnailId),
            [.. (categoryTypeIds ?? []).Select(EventCategoryTypeId.From)],
            [
                .. (termsDocuments ?? [])
                    .OfType<EventTermsDocumentRequest>()
                    .Select(link => new EventTermsLink(
                        TermsDocumentId.From(link.TermsDocumentId),
                        link.Required
                    )),
            ]
        );
    }
}
