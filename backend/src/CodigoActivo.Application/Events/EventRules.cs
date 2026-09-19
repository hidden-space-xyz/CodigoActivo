using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Events;

/// <summary>
/// Applies the domain rules for event.
/// </summary>
public static class EventRules
{
    internal static Result<EventSchedule> ValidateSchedule(
        DateOnly? eventStartsAt,
        DateOnly? eventEndsAt,
        DateTimeOffset? earlySignupStartsAt,
        DateTimeOffset? signupStartsAt,
        DateTimeOffset? signupEndsAt
    )
    {
        if (
            eventStartsAt is not { } eventStart
            || eventEndsAt is not { } eventEnd
            || signupStartsAt is not { } signupStart
            || signupEndsAt is not { } signupEnd
        )
        {
            return Error.BadRequest(ErrorCode.EventScheduleRequired);
        }

        if (eventEnd < eventStart || signupEnd <= signupStart)
        {
            return Error.BadRequest(ErrorCode.EventScheduleInvalidRange);
        }

        if (earlySignupStartsAt is { } earlyStart && earlyStart >= signupStart)
        {
            return Error.BadRequest(ErrorCode.EventEarlySignupNotBeforeSignup);
        }

        if (DateOnly.FromDateTime(signupStart.UtcDateTime) > eventEnd)
        {
            return Error.BadRequest(ErrorCode.EventScheduleInvalidRange);
        }

        return new EventSchedule(
            eventStart,
            eventEnd,
            earlySignupStartsAt?.ToUniversalTime(),
            signupStart.ToUniversalTime(),
            signupEnd.ToUniversalTime()
        );
    }

    /// <summary>
    /// Synchronizes the categories with the supplied identifiers.
    /// </summary>
    /// <param name="ev">The ev value.</param>
    /// <param name="categoryTypeIds">Identifiers of the category type items.</param>
    public static void SyncCategories(Event ev, IReadOnlyList<Guid> categoryTypeIds)
    {
        var desired = categoryTypeIds.Distinct().ToHashSet();

        var removed = ev.Categories.Where(c => !desired.Contains(c.EventCategoryTypeId)).ToList();
        foreach (var existing in removed)
        {
            ev.Categories.Remove(existing);
        }

        var current = ev.Categories.Select(c => c.EventCategoryTypeId).ToHashSet();
        foreach (var categoryTypeId in desired.Except(current))
        {
            ev.Categories.Add(
                new EventCategory { EventId = ev.Id, EventCategoryTypeId = categoryTypeId }
            );
        }
    }

    /// <summary>
    /// Synchronizes the terms documents linked to the event with the supplied requests, keeping
    /// existing acceptance history for documents that stay linked and setting the display order
    /// to the position of each request in the supplied list.
    /// </summary>
    /// <param name="ev">The ev value.</param>
    /// <param name="termsDocuments">The requested terms document links, or <see langword="null"/> when the event has none.</param>
    public static void SyncTermsDocuments(
        Event ev,
        IReadOnlyList<EventTermsDocumentRequest>? termsDocuments
    )
    {
        var desired = termsDocuments ?? [];
        var desiredIds = desired.Select(t => t.TermsDocumentId).ToHashSet();

        var removed = ev
            .TermsDocuments.Where(t => !desiredIds.Contains(t.TermsDocumentId))
            .ToList();
        foreach (var existing in removed)
        {
            ev.TermsDocuments.Remove(existing);
        }

        var current = ev.TermsDocuments.ToDictionary(t => t.TermsDocumentId);
        for (var index = 0; index < desired.Count; index++)
        {
            var request = desired[index];
            if (current.TryGetValue(request.TermsDocumentId, out var existing))
            {
                existing.IsRequired = request.Required;
                existing.DisplayOrder = index;
            }
            else
            {
                ev.TermsDocuments.Add(
                    new EventTermsDocument
                    {
                        EventId = ev.Id,
                        TermsDocumentId = request.TermsDocumentId,
                        IsRequired = request.Required,
                        DisplayOrder = index,
                    }
                );
            }
        }
    }

    internal readonly record struct EventSchedule(
        DateOnly EventStartsAt,
        DateOnly EventEndsAt,
        DateTimeOffset? EarlySignupStartsAt,
        DateTimeOffset SignupStartsAt,
        DateTimeOffset SignupEndsAt
    );
}
