using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Logs.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class MeControllerPersonalDataTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string PersonalDataUrl = "/api/me/personal-data";

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Morning = new(2026, 7, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventId = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid ActivityId = new("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid ThumbnailId = new("dddddddd-0000-0000-0000-000000000003");
    private static readonly Guid TermsDocumentId = new("dddddddd-0000-0000-0000-000000000004");

    private Task SeedParticipationAsync()
    {
        return Factory.SeedAsync(db =>
        {
            db.Files.Add(
                Persisted.As<StoredFile>(
                    new
                    {
                        Id = ThumbnailId,
                        Name = "thumb",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.TermsDocuments.Add(
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = TermsDocumentId,
                        Name = "Uso de imágenes",
                        Description = "{}",
                    }
                )
            );
            db.Events.Add(
                Persisted.As<Event>(
                    new
                    {
                        Id = EventId,
                        Title = "Hackatón",
                        Subtitle = "Sub",
                        Description = "{}",
                        EventStartsAt = new DateOnly(2026, 6, 1),
                        EventEndsAt = new DateOnly(2026, 6, 2),
                        SignupStartsAt = SeededAt,
                        SignupEndsAt = SeededAt.AddDays(10),
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.Activities.Add(
                Persisted.As<Activity>(
                    new
                    {
                        Id = ActivityId,
                        Title = "Taller",
                        Description = "Descripción",
                        Location = "Sala",
                        ActivityStartsAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                        ActivityEndsAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
                        EventId = EventId,
                        Modality = ActivityModality.Presencial,
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.Assignments.AddRange(
                Assignment(TestSeedData.Users.MemberId, ActivityRole.Volunteer),
                Assignment(TestSeedData.Users.MemberChildId, ActivityRole.Participant),
                Assignment(TestSeedData.Users.BlockedId, ActivityRole.Participant)
            );
            db.EventTermsAcceptances.AddRange(
                Decision(
                    TestSeedData.Users.MemberId,
                    HandwrittenSignature.FromStored(TestSeedData.ValidPng())
                ),
                Decision(TestSeedData.Users.BlockedId, null)
            );
            db.NewsConsentChanges.AddRange(
                ConsentChange(TestSeedData.Users.MemberId, true),
                ConsentChange(TestSeedData.Users.BlockedId, false)
            );
            db.AddRange(
                Audit("User", TestSeedData.Users.MemberId, TestSeedData.Users.MemberId),
                Audit("User", TestSeedData.Users.BlockedId, TestSeedData.Users.BlockedId),
                Audit("Anonymous", null, TestSeedData.Users.MemberId)
            );
            return Task.CompletedTask;
        });
    }

    private static Assignment Assignment(Guid userId, ActivityRole role)
    {
        return Persisted.As<Assignment>(
            new
            {
                UserId = userId,
                ActivityId = ActivityId,
                Role = role,
                Status = AssignmentStatus.Confirmed,
                CreatedAt = SeededAt,
            }
        );
    }

    private static EventTermsAcceptance Decision(Guid userId, HandwrittenSignature? signature)
    {
        return Persisted.As<EventTermsAcceptance>(
            new
            {
                EventId = EventId,
                UserId = userId,
                TermsDocumentId = TermsDocumentId,
                Accepted = true,
                DecidedAt = SeededAt,
                Signature = signature,
            }
        );
    }

    private static AuditLogEntry Audit(string actorType, Guid? actor, Guid entity)
    {
        return new AuditLogEntry
        {
            Id = Guid.CreateVersion7(Morning),
            OccurredAt = Morning,
            ActorType = actorType,
            ActorId = actor,
            ClientIp = IPAddress.Parse("203.0.113.7"),
            Action = nameof(AuditAction.ChangePassword),
            EntityType = nameof(AuditEntityType.User),
            EntityId = entity,
            Outcome = nameof(AuditOutcome.Succeeded),
        };
    }

    private static NewsConsentChange ConsentChange(Guid userId, bool granted)
    {
        return Persisted.As<NewsConsentChange>(
            new
            {
                UserId = UserId.From(userId),
                Granted = granted,
                Source = NewsConsentSource.Registration,
                ChangedAt = SeededAt,
            }
        );
    }

    [Fact]
    public async Task PersonalDataAsMemberReturnsTheHouseholdAndNothingOfOtherAccounts()
    {
        await SeedParticipationAsync();
        var client = await LoginAsMemberAsync();

        using var response = await client.GetAsync(TestUri.Rel(PersonalDataUrl), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var data = (await response.ReadJsonAsync<PersonalDataResponse>(Ct))!;
        data.GeneratedAt.Should().Be(Factory.Clock.UtcNow);
        data.Account.Id.Should().Be(TestSeedData.Users.MemberId);
        data.Account.Email.Should().Be(TestSeedData.MemberEmail);
        data.Account.NationalId.Should().Be(TestSeedData.MemberNationalId);
        data.Account.NewsConsent.Should().BeTrue();
        data.Account.AdultDeclaredAt.Should().Be(SeededAt);
        data.NewsConsentChanges.Should()
            .Equal(new PersonalDataConsentChangeResponse(true, "Registration", SeededAt));
        var dependent = data.Dependents.Should().ContainSingle().Subject;
        dependent.Id.Should().Be(TestSeedData.Users.MemberChildId);
        dependent.BirthDate.Should().Be(new DateOnly(2015, 5, 5));
        dependent.GuardianshipDeclaredAt.Should().Be(SeededAt);
        data.Signups.Select(signup => signup.PersonId)
            .Should()
            .BeEquivalentTo([TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId]);
        data.Signups.Should()
            .AllSatisfy(signup =>
            {
                signup.EventTitle.Should().Be("Hackatón");
                signup.ActivityTitle.Should().Be("Taller");
                signup.SignedUpAt.Should().Be(SeededAt);
            });
        var decision = data.TermsDecisions.Should().ContainSingle().Subject;
        decision.DocumentName.Should().Be("Uso de imágenes");
        decision.Accepted.Should().BeTrue();
        decision.Signature.Should().Equal(TestSeedData.ValidPng());
        data.Sessions.Should().ContainSingle();
        data.Operations.Should()
            .Equal(
                new OwnAuditEntryResponse(
                    Morning,
                    AuditAction.ChangePassword,
                    AuditOutcome.Succeeded,
                    "203.0.113.7"
                )
            );
    }

    [Fact]
    public async Task PersonalDataAnonymousUserReturnsUnauthorized()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TestUri.Rel(PersonalDataUrl), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
