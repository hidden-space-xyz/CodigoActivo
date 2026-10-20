using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EventTermsPerPersonTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Guid conditionsId = Guid.NewGuid();
    private readonly Guid consentId = Guid.NewGuid();
    private readonly Guid teenagerId = Guid.NewGuid();

    private async Task<(Guid EventId, Guid ActivityId)> SeedEventAsync()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.TermsDocuments.AddRange(
                Document(conditionsId, "Normas", TermsDocumentKind.Conditions),
                Document(consentId, "Uso de imágenes", TermsDocumentKind.Consent)
            );
            var ev = Persisted.As<Event>(
                new
                {
                    Id = eventId,
                    Title = "Evento",
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
            );
            Persisted.Add(ev.TermsDocuments, Link(conditionsId, required: true, displayOrder: 0));
            Persisted.Add(ev.TermsDocuments, Link(consentId, required: false, displayOrder: 1));
            db.Events.Add(ev);
            db.Activities.Add(
                Persisted.As<Activity>(
                    new
                    {
                        Id = activityId,
                        Title = "Actividad",
                        Description = "Descripcion",
                        Location = "Sala",
                        Modality = ActivityModality.Presencial,
                        ActivityStartsAt = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                        ActivityEndsAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
                        EventId = eventId,
                        ThumbnailId = thumb,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.Users.Add(
                Persisted.As<User>(
                    new
                    {
                        Id = teenagerId,
                        FirstName = "Bea",
                        LastName = "Miembro",
                        BirthDate = Factory.Clock.Today.AddYears(-15),
                        Gender = Gender.Female,
                        ParentId = TestSeedData.Users.MemberId,
                        Status = UserStatus.Dependent,
                        UserType = UserType.Participant,
                        CreatedAt = SeededAt,
                    }
                )
            );
            return Task.CompletedTask;
        });
        return (eventId, activityId);
    }

    private static TermsDocument Document(Guid id, string name, TermsDocumentKind kind)
    {
        return Persisted.As<TermsDocument>(
            new
            {
                Id = id,
                Name = $"{name} {id:N}",
                Description = "{}",
                Kind = kind,
            }
        );
    }

    private static EventTermsDocument Link(Guid termsDocumentId, bool required, int displayOrder)
    {
        return Persisted.As<EventTermsDocument>(
            new
            {
                TermsDocumentId = termsDocumentId,
                IsRequired = required,
                DisplayOrder = displayOrder,
            }
        );
    }

    private static HouseholdAssignmentRequest Member(
        Guid userId,
        params TermsDecisionRequest[] decisions
    )
    {
        return new HouseholdAssignmentRequest(
            userId,
            KnownIds.ActivityRoleTypes.Participant,
            decisions
        );
    }

    private Task<List<EventTermsAcceptance>> DecisionsAsync(Guid eventId)
    {
        return Factory.QueryAsync(db =>
            db.EventTermsAcceptances.Where(a => a.EventId == EventId.From(eventId)).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task AssignHouseholdRecordsTheDecisionsOfEachMemberWithWhoTookThem()
    {
        var (eventId, activityId) = await SeedEventAsync();
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/activities/{activityId}/assign-household",
            new AssignHouseholdRequest([
                Member(
                    TestSeedData.Users.MemberChildId,
                    new TermsDecisionRequest(conditionsId, true, Version: 1),
                    new TermsDecisionRequest(consentId, true, Version: 1)
                ),
                Member(
                    teenagerId,
                    new TermsDecisionRequest(conditionsId, true, Version: 1),
                    new TermsDecisionRequest(consentId, false, Version: 1)
                ),
            ]),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var member = UserId.From(TestSeedData.Users.MemberId);
        var child = UserId.From(TestSeedData.Users.MemberChildId);
        var teenager = UserId.From(teenagerId);
        (await DecisionsAsync(eventId))
            .Select(a => (a.UserId, a.TermsDocumentId.Value, a.Accepted, a.DecidedById))
            .Should()
            .BeEquivalentTo([
                (child, conditionsId, true, member),
                (child, consentId, true, member),
                (teenager, conditionsId, true, member),
                (teenager, consentId, false, teenager),
            ]);
    }

    [Fact]
    public async Task AssignHouseholdMemberWithoutTheRequiredConditionsIsRefusedAndNothingIsSignedUp()
    {
        var (eventId, activityId) = await SeedEventAsync();
        var client = await LoginAsMemberAsync();

        using var response = await client.PostJsonAsync(
            $"/api/activities/{activityId}/assign-household",
            new AssignHouseholdRequest([
                Member(
                    TestSeedData.Users.MemberChildId,
                    new TermsDecisionRequest(conditionsId, true, Version: 1)
                ),
                Member(teenagerId, new TermsDecisionRequest(consentId, true, Version: 1)),
            ]),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.EventTermsAcceptanceRequired);
        (await DecisionsAsync(eventId)).Should().BeEmpty();
        (
            await Factory.QueryAsync(db =>
                db.Activities.Where(a => a.Id == ActivityId.From(activityId))
                    .SelectMany(a => a.Assignments)
                    .CountAsync(Ct)
            )
        )
            .Should()
            .Be(0);
    }
}
