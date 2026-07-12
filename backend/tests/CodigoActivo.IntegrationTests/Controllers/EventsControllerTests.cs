using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
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

    private async Task<Guid> SeedThumbnailAsync()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = id,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = SeededAt,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return id;
    }

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
        Guid? categoryTypeId = null
    )
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId =
            categoryTypeId ?? await SeedCategoryTypeAsync(Guid.NewGuid().ToString("N"));
        var id = Guid.NewGuid();
        var startAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(
                new Event
                {
                    Id = id,
                    Title = title,
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = start,
                    EventEndsAt = end,
                    SignupStartsAt = startAt.AddDays(-10),
                    SignupEndsAt = startAt.AddDays(-1),
                    Featured = featured,
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                    Categories = { new EventCategory { EventCategoryTypeId = categoryId } },
                }
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
            signupStart ?? new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            signupEnd ?? new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero),
            thumbnailId,
            categoryTypeIds
        );
    }

    [Fact]
    public async Task List_Anonymous_ReturnsPagedEnvelopeWithCategories()
    {
        var categoryId = await SeedCategoryTypeAsync("Cultura");
        await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 5),
            title: "Alfa",
            categoryTypeId: categoryId
        );
        var client = CreateClient();

        var response = await client.GetAsync("/api/events", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventListItemResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25);
        var item = page.Items.Should().ContainSingle(e => e.Title == "Alfa").Subject;
        item.Categories.Should()
            .ContainSingle(c => c.CategoryTypeId == categoryId && c.Name == "Cultura");
    }

    [Fact]
    public async Task Get_EventMissing_Returns404EventNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/events/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task PastYears_Anonymous_ReturnsDistinctYearsDescending()
    {
        await SeedEventAsync(new DateOnly(2024, 5, 1), new DateOnly(2024, 6, 1), title: "P24");
        await SeedEventAsync(new DateOnly(2025, 5, 1), new DateOnly(2025, 6, 1), title: "P25a");
        await SeedEventAsync(new DateOnly(2025, 8, 1), new DateOnly(2025, 9, 1), title: "P25b");
        await SeedEventAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10), title: "Futuro");
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/events/past-years",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var years = await response.ReadJsonAsync<IReadOnlyList<int>>(
            TestContext.Current.CancellationToken
        );
        years.Should().Equal(2025, 2024);
    }

    [Fact]
    public async Task Create_AsAdmin_PersistsEventAndReturns201()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync("Taller");
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, [categoryId], title: "Creado");

        var response = await client.PostJsonAsync(
            "/api/events",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.ReadJsonAsync<EventResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Title.Should().Be("Creado");

        var stored = await Factory.QueryAsync(db =>
            db.Events.Include(e => e.Categories)
                .FirstOrDefaultAsync(e => e.Id == created.Id, TestContext.Current.CancellationToken)
        );
        stored!.CreatedBy.Should().Be(TestSeedData.Users.AdminId);
        stored.Categories.Should().ContainSingle(c => c.EventCategoryTypeId == categoryId);
    }

    [Fact]
    public async Task Create_AsMember_ReturnsForbidden()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsMemberAsync();
        var request = BuildCreate(thumbnailId, [categoryId]);

        var response = await client.PostJsonAsync(
            "/api/events",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();
        var request = BuildCreate(Guid.NewGuid(), [Guid.NewGuid()]);

        var response = await client.PostJsonAsync(
            "/api/events",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_BlankTitle_ReturnsValidationError()
    {
        var thumbnailId = await SeedThumbnailAsync();
        var categoryId = await SeedCategoryTypeAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildCreate(thumbnailId, [categoryId], title: "   ");

        var response = await client.PostJsonAsync(
            "/api/events",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Update_AsAdmin_PersistsChanges()
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

        var response = await client.PutJsonAsync(
            $"/api/events/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Events.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Title.Should().Be("Después");
        stored.UpdatedBy.Should().Be(TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task Update_ReplacementThumbnail_DeletesOrphanedOldFile()
    {
        var categoryId = await SeedCategoryTypeAsync("Cascada");
        var id = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 10),
            title: "ConMiniatura",
            categoryTypeId: categoryId
        );
        var oldThumbnailId = (
            await Factory.QueryAsync(db =>
                db.Events.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
        var newThumbnailId = await SeedThumbnailAsync();
        var client = await LoginAsAdminAsync();
        var request = BuildUpdate(newThumbnailId, [categoryId]);

        var response = await client.PutJsonAsync(
            $"/api/events/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var oldFile = await Factory.QueryAsync(db =>
            db.Files.FindAsync([oldThumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        oldFile.Should().BeNull("the replaced thumbnail is orphaned and must be cascade-deleted");
        var newFile = await Factory.QueryAsync(db =>
            db.Files.FindAsync([newThumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        newFile.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesEventAndOrphanedThumbnail()
    {
        var id = await SeedEventAsync(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 5),
            title: "Borrar"
        );
        var thumbnailId = (
            await Factory.QueryAsync(db =>
                db.Events.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
            )
        )!.ThumbnailId;
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/events/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Events.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored.Should().BeNull();
        var file = await Factory.QueryAsync(db =>
            db.Files.FindAsync([thumbnailId], TestContext.Current.CancellationToken).AsTask()
        );
        file.Should()
            .BeNull("the deleted event's thumbnail is orphaned and must be cascade-deleted");
    }

    [Fact]
    public async Task Feature_EventMissing_Returns404EventNotFound()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/events/{Guid.NewGuid()}/feature",
            ct: TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task CategoryTypes_AsAdmin_ReturnsPagedEnvelopeWithSeededTypes()
    {
        await SeedCategoryTypeAsync("Alpha");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/events/categoryType",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(25);
        page.Items.Should().ContainSingle(t => t.Name == "Alpha");
    }

    [Fact]
    public async Task CategoryTypes_NameFilter_MatchesAccentAndCaseInsensitively()
    {
        await SeedCategoryTypeAsync("Robótica", "#112233");
        await SeedCategoryTypeAsync("Charlas", "#445566");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/events/categoryType?name=ROBOTICA",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Name.Should().Be("Robótica");
    }

    [Fact]
    public async Task CategoryTypes_SecondPageOfOne_ReturnsSecondTypeByNameWithTotal()
    {
        await SeedCategoryTypeAsync("Beta", "#222222");
        await SeedCategoryTypeAsync("Alpha", "#111111");
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/events/categoryType?pageSize=1&page=2",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<EventCategoryTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(2);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(1);
        page.Items.Should().ContainSingle().Which.Name.Should().Be("Beta");
    }

    [Fact]
    public async Task CreateCategoryType_AsAdmin_PersistsAndReturnsOk()
    {
        var client = await LoginAsAdminAsync();
        var request = new CreateEventCategoryTypeRequest("Innovación", "#3366cc");

        var response = await client.PostJsonAsync(
            "/api/events/categoryType",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<EventCategoryTypeResponse>(
            TestContext.Current.CancellationToken
        );
        created!.Name.Should().Be("Innovación");

        var stored = await Factory.QueryAsync(db =>
            db.EventCategoryTypes.FindAsync([created.Id], TestContext.Current.CancellationToken)
                .AsTask()
        );
        stored!.Color.Should().Be("#3366cc");
    }

    [Fact]
    public async Task UpdateCategoryType_AsAdmin_PersistsChanges()
    {
        var id = await SeedCategoryTypeAsync("Vieja", "#111111");
        var client = await LoginAsAdminAsync();
        var request = new UpdateEventCategoryTypeRequest("Nueva", "#222222");

        var response = await client.PutJsonAsync(
            $"/api/events/categoryType/{id}",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.EventCategoryTypes.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.Name.Should().Be("Nueva");
        stored.Color.Should().Be("#222222");
    }

    [Fact]
    public async Task DeleteCategoryType_AsAdmin_RemovesIt()
    {
        var id = await SeedCategoryTypeAsync("Efímera");
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/events/categoryType/{id}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.EventCategoryTypes.FindAsync([id], TestContext.Current.CancellationToken).AsTask()
        );
        stored.Should().BeNull();
    }
}
