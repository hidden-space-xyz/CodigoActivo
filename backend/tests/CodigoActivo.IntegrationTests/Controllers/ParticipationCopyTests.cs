using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class ParticipationCopyTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClimbingStart = new(
        2026,
        7,
        10,
        10,
        0,
        0,
        TimeSpan.Zero
    );
    private static readonly DateTimeOffset WorkshopStart = new(
        2026,
        7,
        11,
        10,
        0,
        0,
        TimeSpan.Zero
    );

    private readonly Guid eventId = Guid.NewGuid();
    private readonly Guid climbingId = Guid.NewGuid();
    private readonly Guid workshopId = Guid.NewGuid();
    private readonly Guid imageId = Guid.NewGuid();
    private readonly Guid conditionsId = Guid.NewGuid();

    private static Guid Member => TestSeedData.Users.MemberId;

    private static Guid Child => TestSeedData.Users.MemberChildId;

    private async Task SeedEventAsync(bool withParticipation = true)
    {
        var thumb = await SeedThumbnailAsync();
        await Factory.SeedAsync(db =>
        {
            db.TermsDocuments.AddRange(
                Document(imageId, "Imagen", TermsDocumentKind.Consent),
                Document(conditionsId, "Normas", TermsDocumentKind.Conditions)
            );
            db.Events.Add(
                Persisted.As<Event>(
                    new
                    {
                        Id = eventId,
                        Title = "Feria",
                        Subtitle = "Sub",
                        Description = "{}",
                        EventStartsAt = new DateOnly(2026, 7, 1),
                        EventEndsAt = new DateOnly(2026, 7, 31),
                        SignupStartsAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
                        SignupEndsAt = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
                        ThumbnailId = thumb,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.Activities.AddRange(
                Activity(climbingId, "Escalada", ClimbingStart, thumb),
                Activity(workshopId, "Taller", WorkshopStart, thumb)
            );
            if (withParticipation)
            {
                db.Assignments.AddRange(
                    Assignment(Child, climbingId),
                    Assignment(Member, climbingId),
                    Assignment(Child, workshopId)
                );
                var image = Decision(imageId, accepted: true, null);
                image.Withdraw(
                    UserId.From(Member),
                    UserId.From(TestSeedData.Users.AdminId),
                    SeededAt.AddDays(2)
                );
                db.EventTermsAcceptances.AddRange(
                    image,
                    Decision(
                        conditionsId,
                        accepted: true,
                        HandwrittenSignature.FromStored(TestSeedData.ValidPng())
                    )
                );
            }

            return Task.CompletedTask;
        });
    }

    private static TermsDocument Document(Guid id, string name, TermsDocumentKind kind)
    {
        return Persisted.As<TermsDocument>(
            new
            {
                Id = id,
                Name = $"{name} {id:N}",
                Description = "{\"type\":\"doc\"}",
                RequiresSignature = false,
                Kind = kind,
            }
        );
    }

    private Activity Activity(Guid id, string title, DateTimeOffset startsAt, Guid thumb)
    {
        return Persisted.As<Activity>(
            new
            {
                Id = id,
                Title = title,
                Description = "Descripción",
                Location = $"Sala {title}",
                ActivityStartsAt = startsAt,
                ActivityEndsAt = startsAt.AddHours(2),
                EventId = eventId,
                Modality = CatalogIds.ActivityModalities.ValueOf(
                    KnownIds.ActivityModalityTypes.Online
                ),
                ThumbnailId = thumb,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            }
        );
    }

    private static Assignment Assignment(Guid userId, Guid activityId)
    {
        return Persisted.As<Assignment>(
            new
            {
                UserId = userId,
                ActivityId = activityId,
                Role = CatalogIds.ActivityRoles.ValueOf(KnownIds.ActivityRoleTypes.Participant),
                Status = CatalogIds.AssignmentStatuses.ValueOf(
                    KnownIds.AssignmentStatusTypes.Confirmed
                ),
                CreatedAt = SeededAt,
            }
        );
    }

    private EventTermsAcceptance Decision(
        Guid documentId,
        bool accepted,
        HandwrittenSignature? signature
    )
    {
        return EventTermsAcceptance.Record(
            EventId.From(eventId),
            UserId.From(Child),
            UserId.From(Member),
            TermsDocumentId.From(documentId),
            1,
            accepted,
            signature,
            UserId.From(Member),
            SeededAt
        );
    }

    private async Task<(DeletedParticipation Stored, JsonElement Copy)> SingleCopyAsync()
    {
        var stored = (
            await Factory.QueryAsync(db => db.DeletedParticipations.AsNoTracking().ToListAsync(Ct))
        )
            .Should()
            .ContainSingle()
            .Subject;
        using var document = JsonDocument.Parse(stored.Data);
        return (stored, document.RootElement.Clone());
    }

    private static List<JsonElement> Items(JsonElement array)
    {
        return [.. array.EnumerateArray()];
    }

    private static Guid IdOf(JsonElement element, string property = "id")
    {
        return element.GetProperty(property).GetGuid();
    }

    [Fact]
    public async Task DeleteEventKeepsABlockedCopyOfItsSignupsAndDecisionsWithTheirHistory()
    {
        await SeedEventAsync();
        var admin = await LoginAsAdminAsync();

        using var response = await admin.DeleteWithCsrfAsync($"/api/events/{eventId}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (stored, copy) = await SingleCopyAsync();
        stored.DeletedAt.Should().Be(Factory.Clock.UtcNow);
        copy.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        var deletion = copy.GetProperty("deletion");
        deletion.GetProperty("origin").GetString().Should().Be("EventDeleted");
        IdOf(deletion, "actorId").Should().Be(TestSeedData.Users.AdminId);
        deletion.GetProperty("activityId").ValueKind.Should().Be(JsonValueKind.Null);
        Items(copy.GetProperty("people"))
            .Select(person =>
                (
                    IdOf(person),
                    person.GetProperty("firstName").GetString(),
                    person.GetProperty("lastName").GetString()
                )
            )
            .Should()
            .Equal((Member, "Marta", "Miembro"), (Child, "Mateo", "Miembro"));
        Items(copy.GetProperty("termsDocuments"))
            .Select(document => IdOf(document))
            .Should()
            .Equal(imageId, conditionsId);
        Items(copy.GetProperty("termsDocuments"))[0]
            .GetProperty("textAtDeletion")
            .GetProperty("type")
            .GetString()
            .Should()
            .Be("doc");

        var ev = copy.GetProperty("event");
        IdOf(ev).Should().Be(eventId);
        ev.GetProperty("title").GetString().Should().Be("Feria");
        ev.GetProperty("startsOn").GetString().Should().Be("2026-07-01");
        var decisions = Items(ev.GetProperty("termsDecisions"));
        decisions
            .Select(d => IdOf(d, "documentId"))
            .Should()
            .BeEquivalentTo([imageId, conditionsId]);
        decisions.Should().AllSatisfy(d => IdOf(d, "personId").Should().Be(Child));
        var image = decisions.Single(d => IdOf(d, "documentId") == imageId);
        image.GetProperty("accepted").GetBoolean().Should().BeFalse();
        image.GetProperty("documentVersion").GetInt32().Should().Be(1);
        Items(image.GetProperty("changes"))
            .Select(change =>
                (change.GetProperty("accepted").GetBoolean(), IdOf(change, "recordedBy"))
            )
            .Should()
            .Equal((true, Member), (false, TestSeedData.Users.AdminId));
        decisions
            .Single(d => IdOf(d, "documentId") == conditionsId)
            .GetProperty("signature")
            .GetBytesFromBase64()
            .Should()
            .Equal(TestSeedData.ValidPng());
        Items(ev.GetProperty("activities"))
            .Select(a => (IdOf(a), IdOf(a, "participantId"), a.GetProperty("status").GetString()))
            .Should()
            .BeEquivalentTo([
                (climbingId, Child, "Confirmada"),
                (climbingId, Member, "Confirmada"),
                (workshopId, Child, "Confirmada"),
            ]);
        await Factory.QueryAsync(async db =>
        {
            (await db.EventTermsAcceptances.AnyAsync(a => a.EventId == EventId.From(eventId), Ct))
                .Should()
                .BeFalse();
            (await db.Assignments.AnyAsync(a => a.UserId == UserId.From(Child), Ct))
                .Should()
                .BeFalse();
            return true;
        });
    }

    [Fact]
    public async Task DeleteActivityKeepsOnlyItsSignupsAndLeavesTheDecisions()
    {
        await SeedEventAsync();
        var admin = await LoginAsAdminAsync();

        using var response = await admin.DeleteWithCsrfAsync($"/api/activities/{climbingId}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (_, copy) = await SingleCopyAsync();
        var deletion = copy.GetProperty("deletion");
        deletion.GetProperty("origin").GetString().Should().Be("ActivityDeleted");
        IdOf(deletion, "activityId").Should().Be(climbingId);
        Items(copy.GetProperty("termsDocuments")).Should().BeEmpty();
        var ev = copy.GetProperty("event");
        Items(ev.GetProperty("termsDecisions")).Should().BeEmpty();
        Items(ev.GetProperty("activities"))
            .Select(a => (IdOf(a), IdOf(a, "participantId")))
            .Should()
            .BeEquivalentTo([(climbingId, Child), (climbingId, Member)]);
        (
            await Factory.QueryAsync(db =>
                db.EventTermsAcceptances.CountAsync(a => a.EventId == EventId.From(eventId), Ct)
            )
        )
            .Should()
            .Be(2);
    }

    [Fact]
    public async Task CancelSignupKeepsACopyOfThatSignupOnly()
    {
        await SeedEventAsync();
        var member = await LoginAsMemberAsync();

        using var response = await member.PatchJsonAsync(
            $"/api/activities/{workshopId}/{Child}/unassign",
            ct: Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var (_, copy) = await SingleCopyAsync();
        var deletion = copy.GetProperty("deletion");
        deletion.GetProperty("origin").GetString().Should().Be("SignupCancelled");
        IdOf(deletion, "actorId").Should().Be(Member);
        IdOf(deletion, "personId").Should().Be(Child);
        Items(copy.GetProperty("people")).Select(person => IdOf(person)).Should().Equal(Child);
        Items(copy.GetProperty("event").GetProperty("activities"))
            .Select(a => (IdOf(a), IdOf(a, "participantId")))
            .Should()
            .Equal((workshopId, Child));
    }

    [Fact]
    public async Task DeleteEventWithoutSignupsOrDecisionsKeepsNoCopy()
    {
        await SeedEventAsync(withParticipation: false);
        var admin = await LoginAsAdminAsync();

        using var response = await admin.DeleteWithCsrfAsync($"/api/events/{eventId}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Factory.QueryAsync(db => db.DeletedParticipations.AnyAsync(Ct))).Should().BeFalse();
    }

    [Fact]
    public async Task SaveChangesChangingAStoredCopyIsRefused()
    {
        await SeedEventAsync();
        var admin = await LoginAsAdminAsync();
        using var response = await admin.DeleteWithCsrfAsync($"/api/events/{eventId}", Ct);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var stored = await db.DeletedParticipations.SingleAsync(Ct);
        db.DeletedParticipations.Remove(stored);

        var save = () => db.SaveChangesAsync(Ct);

        await save.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*retention purge*");
    }
}
