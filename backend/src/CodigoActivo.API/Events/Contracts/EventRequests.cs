using System.ComponentModel.DataAnnotations;
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
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
/// <param name="EarlySignupStartsAt">The early signup starts at value.</param>
/// <param name="SignupStartsAt">The signup starts at value.</param>
/// <param name="SignupEndsAt">The signup ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="CategoryTypeIds">Identifiers of the category type items.</param>
/// <param name="TermsDocuments">The requested terms document links, in display order.</param>
public record CreateEventRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(262144)] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<Guid>? CategoryTypeIds,
    IReadOnlyList<EventTermsDocumentRequest>? TermsDocuments
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
/// Contains the client-supplied data used to update the event.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="EventStartsAt">The event starts at value.</param>
/// <param name="EventEndsAt">The event ends at value.</param>
/// <param name="EarlySignupStartsAt">The early signup starts at value.</param>
/// <param name="SignupStartsAt">The signup starts at value.</param>
/// <param name="SignupEndsAt">The signup ends at value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
/// <param name="CategoryTypeIds">Identifiers of the category type items.</param>
/// <param name="TermsDocuments">The requested terms document links, in display order.</param>
public record UpdateEventRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(262144)] string Description,
    [Required] DateOnly? EventStartsAt,
    [Required] DateOnly? EventEndsAt,
    DateTimeOffset? EarlySignupStartsAt,
    [Required] DateTimeOffset? SignupStartsAt,
    [Required] DateTimeOffset? SignupEndsAt,
    Guid ThumbnailId,
    IReadOnlyList<Guid>? CategoryTypeIds,
    IReadOnlyList<EventTermsDocumentRequest>? TermsDocuments
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
/// <param name="Required">Whether accepting the document is mandatory to complete the signup.</param>
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
                .. (termsDocuments ?? []).Select(link => new EventTermsLink(
                    TermsDocumentId.From(link.TermsDocumentId),
                    link.Required
                )),
            ]
        );
    }
}
