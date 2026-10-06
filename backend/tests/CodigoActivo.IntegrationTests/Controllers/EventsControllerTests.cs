using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.API.EventCategories.Contracts;
using CodigoActivo.API.Events.Contracts;
using CodigoActivo.API.TermsDocuments.Contracts;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EventsControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private async Task<Guid> SeedCategoryTypeAsync(
        string name = "Formación",
        string color = "#112233"
    )
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.EventCategoryTypes.Add(
                Persisted.As<EventCategoryType>(
                    new
                    {
                        Id = id,
                        Name = name,
                        Color = color,
                    }
                )
            );
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedEventAsync(
        DateOnly start,
        DateOnly end,
        bool featured = false,
        string title = "Evento",
        string subtitle = "Sub",
        Guid? categoryTypeId = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        DateTimeOffset? signupStartsAt = null,
        DateTimeOffset? signupEndsAt = null,
        Guid? termsDocumentId = null
    )
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryIds =
            categoryTypeIds
            ?? [categoryTypeId ?? await SeedCategoryTypeAsync(Guid.NewGuid().ToString("N"))];
        var id = Guid.NewGuid();
        var startAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await Factory.SeedAsync(db =>
        {
            var ev = Persisted.As<Event>(
                new
                {
                    Id = id,
                    Title = title,
                    Subtitle = subtitle,
                    Description = "{}",
                    EventStartsAt = start,
                    EventEndsAt = end,
                    SignupStartsAt = signupStartsAt ?? startAt.AddDays(-10),
                    SignupEndsAt = signupEndsAt ?? startAt.AddDays(-1),
                    Featured = featured,
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            foreach (var catId in categoryIds)
            {
                Persisted.Add(
                    ev.Categories,
                    Persisted.As<EventCategory>(new { EventCategoryTypeId = catId })
                );
            }

            if (termsDocumentId is { } linkedTermsDocumentId)
            {
                Persisted.Add(
                    ev.TermsDocuments,
                    Persisted.As<EventTermsDocument>(
                        new
                        {
                            TermsDocumentId = linkedTermsDocumentId,
                            IsRequired = true,
                            DisplayOrder = 0,
                        }
                    )
                );
            }

            db.Events.Add(ev);
            return Task.CompletedTask;
        });
        return id;
    }

    private async Task<Guid> SeedTermsDocumentAsync(string? name = null)
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.TermsDocuments.Add(
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = id,
                        Name = name ?? Guid.NewGuid().ToString("N"),
                        Description = "{}",
                    }
                )
            );
            return Task.CompletedTask;
        });
        return id;
    }

    private static CreateEventRequest BuildCreate(
        Guid thumbnailId,
        IReadOnlyList<Guid>? categoryTypeIds,
        DateOnly? start = null,
        DateOnly? end = null,
        DateTimeOffset? earlySignupStart = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        string title = "Nuevo evento",
        string subtitle = "Subtítulo"
    )
    {
        return new CreateEventRequest(
            title,
            subtitle,
            "{}",
            start ?? new DateOnly(2026, 8, 1),
            end ?? new DateOnly(2026, 8, 10),
            earlySignupStart,
            signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            thumbnailId,
            categoryTypeIds,
            null
        );
    }

    private static UpdateEventRequest BuildUpdate(
        Guid thumbnailId,
        IReadOnlyList<Guid>? categoryTypeIds,
        DateOnly? start = null,
        DateOnly? end = null,
        DateTimeOffset? earlySignupStart = null,
        DateTimeOffset? signupStart = null,
        DateTimeOffset? signupEnd = null,
        string title = "Evento editado"
    )
    {
        return new UpdateEventRequest(
            title,
            "Subtítulo",
            "{}",
            start ?? new DateOnly(2026, 8, 1),
            end ?? new DateOnly(2026, 8, 10),
            earlySignupStart,
            signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            thumbnailId,
            categoryTypeIds,
            null
        );
    }

    [Fact]
    public async Task ListAsyncAnonymousReturnsPagedEnvelopeWithCategories()
    {
        var categoryId = await SeedCategoryTypeAsync("Cultura");
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 5),
            title: "Alfa",
            categoryTypeId: categoryId
        );
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/events"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25);
        var item = page.Items.Should().ContainSingle(e => e.Title == "Alfa").Subject;
        item.Categories.Should()
            .ContainSingle(c => c.CategoryTypeId == categoryId && c.Name == "Cultura");
    }

    [Fact]
    public async Task GetAsyncEventMissingReturns404EventNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{Guid.NewGuid()}"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task PastYearsAsyncAnonymousReturnsDistinctYearsDescending()
    {
        await SeedEventAsync(new DateOnly(2024, 5, 1), new DateOnly(2024, 6, 1), title: "P24");
        await SeedEventAsync(new DateOnly(2025, 5, 1), new DateOnly(2025, 6, 1), title: "P25a");
        await SeedEventAsync(new DateOnly(2025, 8, 1), new DateOnly(2025, 9, 1), title: "P25b");
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10), title: "Futuro");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/events/past-years"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var years = await response.ReadJsonAsync<IReadOnlyList<int>>(Ct);
        years.Should().Equal(2025, 2024);
    }

    [Fact]
    public async Task PastCategoriesAsyncAnonymousReturnsCategoriesOfPastEventsOrderedByName()
    {
        var talleres = await SeedCategoryTypeAsync("Talleres", "#AA0000");
        var charlas = await SeedCategoryTypeAsync("Charlas", "#00AA00");
        var futuro = await SeedCategoryTypeAsync("Futuro");
        await SeedCategoryTypeAsync("Sin eventos");
        await SeedEventAsync(
            new DateOnly(2025, 5, 1),
            new DateOnly(2025, 5, 2),
            categoryTypeIds: [talleres, charlas]
        );
        await SeedEventAsync(
            new DateOnly(2024, 5, 1),
            new DateOnly(2024, 5, 2),
            categoryTypeIds: [talleres]
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            categoryTypeIds: [futuro, talleres]
        );
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/events/past-categories"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.ReadJsonAsync<IReadOnlyList<EventCategoryTypeResponse>>(Ct);
        categories
            .Should()
            .Equal(
                new EventCategoryTypeResponse(charlas, "Charlas", "#00AA00"),
                new EventCategoryTypeResponse(talleres, "Talleres", "#AA0000")
            );
    }

    [Fact]
    public async Task ListAsyncSearchMatchesTitleOrSubtitleIgnoringCaseAndAccents()
    {
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            title: "Robótica creativa",
            subtitle: "Taller"
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 4),
            title: "Campus",
            subtitle: "Talleres de ROBÓTICA"
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 6),
            title: "Ajedrez",
            subtitle: "Torneo"
        );
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/events?search=robotica"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Total.Should().Be(2);
        page.Items.Select(e => e.Title).Should().BeEquivalentTo("Robótica creativa", "Campus");
    }

    [Fact]
    public async Task ListAsyncSortByCategoriesOrdersByMinimumCategoryName()
    {
        var ajedrez = await SeedCategoryTypeAsync("Ajedrez");
        var charla = await SeedCategoryTypeAsync("Charla");
        var musica = await SeedCategoryTypeAsync("Música");
        var taller = await SeedCategoryTypeAsync("Taller");
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            title: "Uno",
            categoryTypeIds: [taller, charla]
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 4),
            title: "Dos",
            categoryTypeIds: [musica]
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 6),
            title: "Tres",
            categoryTypeIds: [taller, ajedrez]
        );
        var client = CreateClient();

        var ascending = await client.GetAsync(TestUri.Rel("/api/events?sort=categories"), Ct);
        var descending = await client.GetAsync(TestUri.Rel("/api/events?sort=-categories"), Ct);

        ascending.StatusCode.Should().Be(HttpStatusCode.OK);
        var ascendingPage = await ascending.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        ascendingPage!.Items.Select(e => e.Title).Should().Equal("Tres", "Uno", "Dos");

        descending.StatusCode.Should().Be(HttpStatusCode.OK);
        var descendingPage = await descending.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        descendingPage!.Items.Select(e => e.Title).Should().Equal("Dos", "Uno", "Tres");
    }

    [Fact]
    public async Task ListSortBySignupDatesOrdersIndependentlyOfEventDates()
    {
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            title: "Primero",
            signupStartsAt: new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            signupEndsAt: new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero)
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 11),
            title: "Segundo",
            signupStartsAt: new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero),
            signupEndsAt: new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero)
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 5),
            new DateOnly(2026, 8, 6),
            title: "Tercero",
            signupStartsAt: new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero),
            signupEndsAt: new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero)
        );
        var client = CreateClient();

        var byStart = await client.GetAsync(TestUri.Rel("/api/events?sort=signupStartsAt"), Ct);
        var byEnd = await client.GetAsync(TestUri.Rel("/api/events?sort=-signupEndsAt"), Ct);

        byStart.StatusCode.Should().Be(HttpStatusCode.OK);
        var byStartPage = await byStart.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        byStartPage!.Items.Select(e => e.Title).Should().Equal("Segundo", "Tercero", "Primero");

        byEnd.StatusCode.Should().Be(HttpStatusCode.OK);
        var byEndPage = await byEnd.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        byEndPage!.Items.Select(e => e.Title).Should().Equal("Segundo", "Primero", "Tercero");
    }

    [Fact]
    public async Task ListFilterByCategoryTypeIdReturnsOnlyEventsWithThatCategory()
    {
        var robotica = await SeedCategoryTypeAsync("Robótica");
        var charlas = await SeedCategoryTypeAsync("Charlas");
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            title: "ConRobotica",
            categoryTypeIds: [robotica, charlas]
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 4),
            title: "SoloCharlas",
            categoryTypeIds: [charlas]
        );
        var client = CreateClient();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/events?categoryTypeId={robotica}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(e => e.Title == "ConRobotica");
    }

    [Fact]
    public async Task ListFilterByEventDateRangeMatchesEventsOverlappingRange()
    {
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), title: "Corto");
        await SeedEventAsync(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 12), title: "Tardio");
        await SeedEventAsync(new DateOnly(2026, 8, 4), new DateOnly(2026, 8, 11), title: "Largo");
        var client = CreateClient();

        var rangeResponse = await client.GetAsync(
            TestUri.Rel("/api/events?eventDateFrom=2026-08-06&eventDateTo=2026-08-10"),
            Ct
        );
        var boundaryResponse = await client.GetAsync(
            TestUri.Rel("/api/events?eventDateTo=2026-08-01"),
            Ct
        );

        rangeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rangePage = await rangeResponse.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        rangePage!.Total.Should().Be(2);
        rangePage.Items.Select(e => e.Title).Should().BeEquivalentTo("Tardio", "Largo");

        boundaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var boundaryPage = await boundaryResponse.ReadJsonAsync<PagedResult<EventListItemResponse>>(
            Ct
        );
        boundaryPage!.Total.Should().Be(1);
        boundaryPage.Items.Should().ContainSingle(e => e.Title == "Corto");
    }

    [Fact]
    public async Task ListSignupFromFilterUsesAppTimezoneDayLowerBound()
    {
        Factory.Clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            title: "EnLimite",
            signupStartsAt: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            signupEndsAt: new DateTimeOffset(2026, 7, 9, 22, 0, 0, TimeSpan.Zero)
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 4),
            title: "Anterior",
            signupStartsAt: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            signupEndsAt: new DateTimeOffset(2026, 7, 9, 21, 59, 59, TimeSpan.Zero)
        );
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/events?signupFrom=2026-07-10"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(e => e.Title == "EnLimite");
    }

    [Fact]
    public async Task ListSignupToFilterUsesAppTimezoneDayUpperBound()
    {
        Factory.Clock.TimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "UTC+02",
            TimeSpan.FromHours(2),
            "UTC+02",
            "UTC+02"
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            title: "DentroDelDia",
            signupStartsAt: new DateTimeOffset(2026, 7, 10, 21, 59, 59, TimeSpan.Zero),
            signupEndsAt: new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        );
        await SeedEventAsync(
            new DateOnly(2026, 8, 3),
            new DateOnly(2026, 8, 4),
            title: "DiaSiguiente",
            signupStartsAt: new DateTimeOffset(2026, 7, 10, 22, 0, 0, TimeSpan.Zero),
            signupEndsAt: new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero)
        );
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/events?signupTo=2026-07-10"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(e => e.Title == "DentroDelDia");
    }

    [Fact]
    public async Task ListFilterByYearUsesEventStartBoundaries()
    {
        await SeedEventAsync(
            new DateOnly(2025, 12, 31),
            new DateOnly(2026, 1, 2),
            title: "Nochevieja"
        );
        await SeedEventAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), title: "AnoNuevo");
        var client = CreateClient();

        var previousYearResponse = await client.GetAsync(TestUri.Rel("/api/events?year=2025"), Ct);
        var currentYearResponse = await client.GetAsync(TestUri.Rel("/api/events?year=2026"), Ct);

        previousYearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var previousYearPage = await previousYearResponse.ReadJsonAsync<
            PagedResult<EventListItemResponse>
        >(Ct);
        previousYearPage!.Total.Should().Be(1);
        previousYearPage.Items.Should().ContainSingle(e => e.Title == "Nochevieja");

        currentYearResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentYearPage = await currentYearResponse.ReadJsonAsync<
            PagedResult<EventListItemResponse>
        >(Ct);
        currentYearPage!.Total.Should().Be(1);
        currentYearPage.Items.Should().ContainSingle(e => e.Title == "AnoNuevo");
    }

    [Fact]
    public async Task ListYearZeroReturnsEmptyPage()
    {
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), title: "Alguno");
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/events?year=0"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>(Ct);
        page!.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsAdminPersistsEventAndReturns201()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync("Taller");
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, [categoryId], title: "Creado");

        var response = await client.PostJsonAsync("/api/events", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<EventResponse>(Ct);
        created!.Title.Should().Be("Creado");

        var stored = await Factory.QueryAsync(db =>
            db.Events.Include(e => e.Categories)
                .FirstOrDefaultAsync(e => e.Id == EventId.From(created.Id), Ct)
        );
        stored!.CreatedBy.Value.Should().Be(TestSeedData.Users.AdminId);
        stored
            .Categories.Should()
            .ContainSingle(c => c.EventCategoryTypeId == EventCategoryTypeId.From(categoryId));
    }

    [Fact]
    public async Task CreateAsMemberReturnsForbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsMemberAsync();
        var request = BuildCreate(thumbnailId, [categoryId]);

        var response = await client.PostJsonAsync("/api/events", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();
        var request = BuildCreate(Guid.NewGuid(), [Guid.NewGuid()]);

        var response = await client.PostJsonAsync("/api/events", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBlankTitleReturnsValidationError()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, [categoryId], title: "   ");

        var response = await client.PostJsonAsync("/api/events", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task UpdateAsAdminPersistsChanges()
    {
        var categoryId = await SeedCategoryTypeAsync("Original");
        var id = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 10),
            title: "Antes",
            categoryTypeId: categoryId
        );
        var thumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildUpdate(thumbnailId, [categoryId], title: "Después");

        var response = await client.PutJsonAsync($"/api/events/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<Event>(id);
        stored!.Title.Should().Be("Después");
        stored.UpdatedBy.Should().Be(UserId.From(TestSeedData.Users.AdminId));
    }

    [Fact]
    public async Task UpdateReplacementThumbnailDeletesOrphanedOldFile()
    {
        var categoryId = await SeedCategoryTypeAsync("Cascada");
        var id = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 10),
            title: "ConMiniatura",
            categoryTypeId: categoryId
        );
        var oldThumbnailId = (await FindAsync<Event>(id))!.ThumbnailId;
        var newThumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildUpdate(newThumbnailId, [categoryId]);

        var response = await client.PutJsonAsync($"/api/events/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldFile = await FindAsync<StoredFile>(oldThumbnailId.Value);
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await FindAsync<StoredFile>(newThumbnailId);
        newFile.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsAdminRemovesEventAndOrphanedThumbnail()
    {
        var id = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 5),
            title: "Borrar"
        );
        var thumbnailId = (await FindAsync<Event>(id))!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/{id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<Event>(id);
        stored.Should().BeNull();
        var file = await FindAsync<StoredFile>(thumbnailId.Value);
        file.Should()
            .BeNull("the deleted event's thumbnail is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task FeatureAsAdminLeavesOnlyTheChosenEventFeaturedWithoutAnEdit()
    {
        var previousId = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            featured: true
        );
        var chosenId = await SeedEventAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2));
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync($"/api/events/{chosenId}/feature", ct: Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var chosen = await FindAsync<Event>(chosenId);
        var previous = await FindAsync<Event>(previousId);
        chosen!.Featured.Should().BeTrue();
        previous!.Featured.Should().BeFalse();
        chosen.UpdatedAt.Should().BeNull();
        previous.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task FeatureEventMissingReturns404EventNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync($"/api/events/{Guid.NewGuid()}/feature", ct: Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task CategoryTypesAsAdminReturnsPagedEnvelopeWithSeededTypes()
    {
        await SeedCategoryTypeAsync("Alpha");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/events/categoryType"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25);
        page.Items.Should().ContainSingle(t => t.Name == "Alpha");
    }

    [Fact]
    public async Task CategoryTypesNameFilterMatchesAccentAndCaseInsensitively()
    {
        await SeedCategoryTypeAsync("Robótica", "#112233");
        await SeedCategoryTypeAsync("Charlas", "#445566");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel("/api/events/categoryType?name=ROBOTICA"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Name.Should().Be("Robótica");
    }

    [Fact]
    public async Task CategoryTypesColorFilterMatchesCaseInsensitively()
    {
        await SeedCategoryTypeAsync("Robótica", "#AABB01");
        await SeedCategoryTypeAsync("Charlas", "#CCDD02");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel("/api/events/categoryType?color=aabb01"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Name.Should().Be("Robótica");
    }

    [Fact]
    public async Task CategoryTypesSortByColorOrdersByColorInsteadOfName()
    {
        await SeedCategoryTypeAsync("Alpha", "#333333");
        await SeedCategoryTypeAsync("Beta", "#111111");
        await SeedCategoryTypeAsync("Gamma", "#222222");
        var client = await LoginAsAdminAsync();

        var ascending = await client.GetAsync(
            TestUri.Rel("/api/events/categoryType?sort=color"),
            Ct
        );
        var descending = await client.GetAsync(
            TestUri.Rel("/api/events/categoryType?sort=-color"),
            Ct
        );

        ascending.StatusCode.Should().Be(HttpStatusCode.OK);
        var ascendingPage = await ascending.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(
            Ct
        );
        ascendingPage!.Items.Select(t => t.Name).Should().Equal("Beta", "Gamma", "Alpha");

        descending.StatusCode.Should().Be(HttpStatusCode.OK);
        var descendingPage = await descending.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(
            Ct
        );
        descendingPage!.Items.Select(t => t.Name).Should().Equal("Alpha", "Gamma", "Beta");
    }

    [Fact]
    public async Task DeleteCategoryTypeMissingIdReturns404WithErrorCode()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/events/categoryType/{Guid.NewGuid()}",
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.EventCategoryTypeNotFound);
    }

    [Fact]
    public async Task CategoryTypesSecondPageOfOneReturnsSecondTypeByNameWithTotal()
    {
        await SeedCategoryTypeAsync("Beta", "#222222");
        await SeedCategoryTypeAsync("Alpha", "#111111");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel("/api/events/categoryType?pageSize=1&page=2"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(Ct);
        page!.Total.Should().Be(2);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Name.Should().Be("Beta");
    }

    [Fact]
    public async Task CreateCategoryTypeAsAdminPersistsAndReturnsOk()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateEventCategoryTypeRequest("Innovación", "#3366cc");

        var response = await client.PostJsonAsync("/api/events/categoryType", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<EventCategoryTypeResponse>(Ct);
        created!.Name.Should().Be("Innovación");

        var stored = await FindAsync<EventCategoryType>(created.Id);
        stored!.Color.Should().Be("#3366cc");
    }

    [Fact]
    public async Task UpdateCategoryTypeAsAdminPersistsChanges()
    {
        var id = await SeedCategoryTypeAsync("Vieja", "#111111");
        var client = await LoginAsAdminAsync();
        var request = new UpdateEventCategoryTypeRequest("Nueva", "#222222");

        var response = await client.PutJsonAsync($"/api/events/categoryType/{id}", request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<EventCategoryType>(id);
        stored!.Name.Should().Be("Nueva");
        stored.Color.Should().Be("#222222");
    }

    [Fact]
    public async Task DeleteCategoryTypeAsAdminRemovesIt()
    {
        var id = await SeedCategoryTypeAsync("Efímera");
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/categoryType/{id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<EventCategoryType>(id);
        stored.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCategoryTypeInUseRemovesItFromTheEvents()
    {
        var id = await SeedCategoryTypeAsync("En uso");
        var keptId = await SeedCategoryTypeAsync("Conservada");
        var eventId = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            categoryTypeIds: [id, keptId]
        );
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/categoryType/{id}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var remaining = await Factory.QueryAsync(db =>
            db.EventCategories.Where(c => c.EventId == EventId.From(eventId))
                .Select(c => c.EventCategoryTypeId)
                .ToListAsync(Ct)
        );
        remaining.Should().Equal(EventCategoryTypeId.From(keptId));
    }

    [Fact]
    public async Task DeleteCategoryTypeOnlyCategoryOfAnEventReturnsConflictAndKeepsIt()
    {
        var id = await SeedCategoryTypeAsync("Única");
        var eventId = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            categoryTypeIds: [id]
        );
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/events/categoryType/{id}", Ct);

        await response.ShouldBeConflictAsync(ErrorCode.EventCategoryTypeOnlyCategoryOfEvent);
        (await FindAsync<EventCategoryType>(id)).Should().NotBeNull();
        var remaining = await Factory.QueryAsync(db =>
            db.EventCategories.Where(c => c.EventId == EventId.From(eventId))
                .Select(c => c.EventCategoryTypeId)
                .ToListAsync(Ct)
        );
        remaining.Should().Equal(EventCategoryTypeId.From(id));
    }

    [Theory]
    [InlineData("talleres")]
    [InlineData("TALLERES")]
    public async Task CreateCategoryTypeNameDifferingOnlyInCaseReturnsConflict(string name)
    {
        await SeedCategoryTypeAsync("Talleres");
        var client = await LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            "/api/events/categoryType",
            new CreateEventCategoryTypeRequest(name, "#3366cc"),
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.EventCategoryTypeNameAlreadyExists);
    }

    [Fact]
    public async Task CreateCategoryTypeNameWithLikeWildcardsIsComparedLiterally()
    {
        await SeedCategoryTypeAsync("Robótica");
        var client = await LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            "/api/events/categoryType",
            new CreateEventCategoryTypeRequest("Rob_tica%", "#3366cc"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateSignupClosingAfterTheEventReturnsBadRequest()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync("Taller");
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(
            thumbnailId,
            [categoryId],
            signupEnd: new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero)
        );

        var response = await client.PostJsonAsync("/api/events", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.EventSignupEndsAfterEvent);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:secret@evil.example")]
    public async Task CreateDescriptionWithUnsafeLinkReturnsBadRequest(string href)
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync("Taller");
        var client = await LoginAsAdminAsync();
        var description =
            "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\""
            + href
            + "\"}}]}]}]}";
        var request = BuildCreate(thumbnailId, [categoryId]) with { Description = description };

        var response = await client.PostJsonAsync("/api/events", request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task TermsStateAnonymousReturnsUnauthorized()
    {
        var eventId = await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2));
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{eventId}/terms"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TermsStateAsMemberReturnsOkWithUndecidedRequiredDocument()
    {
        var termsDocumentId = await SeedTermsDocumentAsync("Reglamento");
        var eventId = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 2),
            termsDocumentId: termsDocumentId
        );
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/events/{eventId}/terms"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.ReadJsonAsync<EventTermsStateResponse>(Ct);
        var document = state!.Documents.Should().ContainSingle().Subject;
        document.TermsDocumentId.Should().Be(termsDocumentId);
        document.Required.Should().BeTrue();
        document.Accepted.Should().BeNull();
        document.DecidedAt.Should().BeNull();
        state
            .SignupBlocked.Should()
            .BeTrue("the member has not yet decided on the event's only, required document");
    }

    [Fact]
    public async Task TermsDocumentWithAnImageIsRefusedOnCreateAndUpdate()
    {
        const string ImageDocument =
            "{\"type\":\"doc\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"/api/files/0f8fad5b-d9cb-469f-a165-70867728950e/content\"}}]}";
        var termsDocumentId = await SeedTermsDocumentAsync("Reglamento");
        var admin = await LoginAsAdminAsync();

        using var created = await admin.PostJsonAsync(
            "/api/events/termsDocument",
            new CreateTermsDocumentRequest("Con imagen", ImageDocument),
            Ct
        );
        using var updated = await admin.PutJsonAsync(
            $"/api/events/termsDocument/{termsDocumentId}",
            new UpdateTermsDocumentRequest("Reglamento", ImageDocument),
            Ct
        );

        await created.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
        await updated.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
        (await FindAsync<TermsDocument>(termsDocumentId))!.Description!.Json.Should().Be("{}");
        await Factory.QueryAsync(async db =>
        {
            (await db.TermsDocuments.AnyAsync(d => d.Name == "Con imagen", Ct)).Should().BeFalse();
            return true;
        });
    }
}
