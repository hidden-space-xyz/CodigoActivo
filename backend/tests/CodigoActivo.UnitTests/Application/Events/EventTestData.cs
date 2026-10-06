using CodigoActivo.API.Events.Contracts;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.Events;

internal static class EventTestData
{
    public static Event NewEvent(
        string title = "Hackathon",
        string subtitle = "Innovación",
        DateOnly? starts = null,
        DateOnly? ends = null,
        bool featured = false,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        string description = "{}",
        IReadOnlyList<EventCategoryTypeId>? categoryTypeIds = null
    )
    {
        var ev = Event.Create(
            new EventContent(
                title,
                subtitle,
                RichText.From(description),
                StoredFileId.From(Guid.NewGuid())
            ),
            EventSchedule
                .Create(
                    starts ?? new DateOnly(2026, 8, 1),
                    ends ?? new DateOnly(2026, 8, 2),
                    null,
                    signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                    signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
                )
                .Value,
            EventCategorySelection.Create(categoryTypeIds ?? [EventCategoryTypeId.New()]).Value,
            EventTermsLinks.None,
            UserId.From(Guid.NewGuid()),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        if (featured)
        {
            ev.Feature();
        }

        return ev;
    }

    public static EventRow NewEventRow(
        string title = "Hackathon",
        string subtitle = "Innovación",
        DateOnly? starts = null,
        DateOnly? ends = null,
        bool featured = false,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null
    )
    {
        var start = starts ?? new DateOnly(2026, 8, 1);
        var end = ends ?? new DateOnly(2026, 8, 2);
        return new EventRow
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = "{}",
            EventStartsAt = start,
            EventEndsAt = end,
            SignupStartsAt = signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt = signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static EventCategoryRow NewCategoryRow(
        Guid eventId,
        Guid categoryTypeId,
        string name,
        string color = "#112233"
    )
    {
        return new()
        {
            EventId = eventId,
            EventCategoryTypeId = categoryTypeId,
            EventCategoryType = new EventCategoryTypeRow
            {
                Id = categoryTypeId,
                Name = name,
                Color = color,
            },
        };
    }

    public static EventRow WithCategory(EventRow ev, Guid categoryTypeId, string name)
    {
        ev.Categories.Add(NewCategoryRow(ev.Id, categoryTypeId, name));
        return ev;
    }

    public static EventCategoryTypeRow NewCategoryTypeRow(string name, string color = "#000000")
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Color = color,
        };
    }

    public static CreateEventRequest CreateReq(
        DateOnly? eventStart = null,
        DateOnly? eventEnd = null,
        DateTimeOffset? earlySignupStart = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        Guid? thumbnailId = null,
        IReadOnlyList<EventTermsDocumentRequest>? termsDocuments = null
    )
    {
        return new(
            Title: "  Hackathon  ",
            Subtitle: "  Innovación  ",
            Description: "{}",
            EventStartsAt: eventStart ?? new DateOnly(2026, 8, 1),
            EventEndsAt: eventEnd ?? new DateOnly(2026, 8, 3),
            EarlySignupStartsAt: earlySignupStart,
            SignupStartsAt: signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt: signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId: thumbnailId ?? Guid.NewGuid(),
            CategoryTypeIds: categoryTypeIds,
            TermsDocuments: termsDocuments
        );
    }

    public static UpdateEventRequest UpdateReq(
        DateOnly? eventStart = null,
        DateOnly? eventEnd = null,
        DateTimeOffset? earlySignupStart = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        Guid? thumbnailId = null,
        string title = "  New title  ",
        string description = "{}",
        IReadOnlyList<EventTermsDocumentRequest>? termsDocuments = null
    )
    {
        return new(
            Title: title,
            Subtitle: "  New subtitle  ",
            Description: description,
            EventStartsAt: eventStart ?? new DateOnly(2026, 8, 1),
            EventEndsAt: eventEnd ?? new DateOnly(2026, 8, 3),
            EarlySignupStartsAt: earlySignupStart,
            SignupStartsAt: signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt: signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            ThumbnailId: thumbnailId ?? Guid.NewGuid(),
            CategoryTypeIds: categoryTypeIds,
            TermsDocuments: termsDocuments
        );
    }

    public static void HasCategoryCount(this IEventCategoryTypeRepository categoryTypes, int count)
    {
        categoryTypes
            .CountExistingAsync(
                Arg.Any<IReadOnlyCollection<EventCategoryTypeId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(count);
    }

    public static void CategoryTypeNameTaken(
        this IEventCategoryTypeRepository categoryTypes,
        bool taken
    )
    {
        categoryTypes
            .NameExistsAsync(
                Arg.Any<string>(),
                Arg.Any<EventCategoryTypeId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(taken);
    }

    public static TermsDocument NewTermsDocument(
        string name = "Términos generales",
        string description = "{}"
    )
    {
        return Persisted.As<TermsDocument>(
            new
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
            }
        );
    }

    public static TermsDocumentRow NewTermsDocumentRow(
        string name = "Términos generales",
        string description = "{}"
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
        };
    }

    public static void TermsDocumentExists(
        this ITermsDocumentRepository termsDocuments,
        bool exists
    )
    {
        termsDocuments
            .NameExistsAsync(
                Arg.Any<string>(),
                Arg.Any<TermsDocumentId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);
    }

    public static void TermsDocumentFound(
        this ITermsDocumentRepository termsDocuments,
        TermsDocument? termsDocument
    )
    {
        termsDocuments
            .GetByIdAsync(Arg.Any<TermsDocumentId>(), Arg.Any<CancellationToken>())
            .Returns(termsDocument);
    }

    public static void TermsDocumentInUse(this IEventRepository events, bool inUse)
    {
        events
            .LinksTermsDocumentAsync(Arg.Any<TermsDocumentId>(), Arg.Any<CancellationToken>())
            .Returns(inUse);
    }

    public static void TermsDocumentAccepted(
        this IEventTermsAcceptanceRepository termsAcceptances,
        bool accepted
    )
    {
        termsAcceptances
            .AnyForDocumentAsync(Arg.Any<TermsDocumentId>(), Arg.Any<CancellationToken>())
            .Returns(accepted);
    }
}
