using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
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
                new EventCategoryType
                {
                    Id = id,
                    Name = name,
                    Color = color,
                }
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
        Guid? categoryTypeId = null,
        IReadOnlyList<Guid>? categoryTypeIds = null,
        DateTimeOffset? signupStartsAt = null,
        DateTimeOffset? signupEndsAt = null
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
            var ev = new Event
            {
                Id = id,
                Title = title,
                Subtitle = "Sub",
                Description = "{}",
                EventStartsAt = start,
                EventEndsAt = end,
                SignupStartsAt = signupStartsAt ?? startAt.AddDays(-10),
                SignupEndsAt = signupEndsAt ?? startAt.AddDays(-1),
                Featured = featured,
                ThumbnailId = thumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            };
            foreach (var catId in categoryIds)
            {
                ev.Categories.Add(new EventCategory { EventCategoryTypeId = catId });
            }

            db.Events.Add(ev);
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
            categoryTypeIds
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
            categoryTypeIds
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

        var response = await client.GetAsync(TestUri.Rel($"/api/events?categoryTypeId={robotica}"), Ct);

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
        var boundaryResponse = await client.GetAsync(TestUri.Rel("/api/events?eventDateTo=2026-08-01"), Ct);

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
            db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.Id == created.Id, Ct)
        );
        stored!.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        stored.Categories.Should().ContainSingle(c => c.EventCategoryTypeId == categoryId);
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
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
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
        var oldFile = await FindAsync<FileEntity>(oldThumbnailId);
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await FindAsync<FileEntity>(newThumbnailId);
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
        var file = await FindAsync<FileEntity>(thumbnailId);
        file.Should()
            .BeNull("the deleted event's thumbnail is orphaned and must be cascade-deleted");
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

        var response = await client.GetAsync(TestUri.Rel("/api/events/categoryType?name=ROBOTICA"), Ct);

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

        var response = await client.GetAsync(TestUri.Rel("/api/events/categoryType?color=aabb01"), Ct);

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

        var ascending = await client.GetAsync(TestUri.Rel("/api/events/categoryType?sort=color"), Ct);
        var descending = await client.GetAsync(TestUri.Rel("/api/events/categoryType?sort=-color"), Ct);

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

        var response = await client.GetAsync(TestUri.Rel("/api/events/categoryType?pageSize=1&page=2"), Ct);

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

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
}
