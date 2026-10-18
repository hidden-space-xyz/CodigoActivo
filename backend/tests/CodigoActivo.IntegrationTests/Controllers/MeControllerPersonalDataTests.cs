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
    private static readonly Guid OtherChildId = new("dddddddd-0000-0000-0000-000000000005");

    private Task SeedParticipationAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.FindAsync([UserId.From(TestSeedData.Users.MemberId)], Ct);
            Persisted.Overwrite(
                member!,
                new
                {
                    EmailChange = new EmailChange(
                        EmailAddress.FromStored("nueva@codigoactivo.test"),
                        "hash",
                        SeededAt.AddDays(1)
                    ),
                }
            );
            db.Users.Add(
                Persisted.As<User>(
                    new
                    {
                        Id = OtherChildId,
                        FirstName = "Olga",
                        LastName = "Bloqueado",
                        BirthDate = new DateOnly(2016, 6, 6),
                        Gender = Gender.Female,
                        ParentId = TestSeedData.Users.BlockedId,
                        Status = UserStatus.Dependent,
                        UserType = UserType.Participant,
                        CreatedAt = SeededAt,
                    }
                )
            );
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
                Assignment(TestSeedData.Users.BlockedId, ActivityRole.Participant),
                Assignment(OtherChildId, ActivityRole.Participant)
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
                Audit(0, AuditAction.Login, null, TestSeedData.Users.MemberId),
                Audit(
                    1,
                    AuditAction.ChangePassword,
                    TestSeedData.Users.MemberId,
                    TestSeedData.Users.MemberId
                ),
                Audit(
                    2,
                    AuditAction.ChangeUserType,
                    TestSeedData.Users.AdminId,
                    TestSeedData.Users.MemberId
                ),
                Audit(
                    3,
                    AuditAction.ChangeAssignmentStatus,
                    TestSeedData.Users.AdminId,
                    null,
                    TestSeedData.Users.MemberChildId
                ),
                Audit(
                    4,
                    AuditAction.ChangePassword,
                    TestSeedData.Users.BlockedId,
                    TestSeedData.Users.BlockedId
                ),
                Audit(
                    5,
                    AuditAction.ChangeAssignmentStatus,
                    TestSeedData.Users.AdminId,
                    null,
                    OtherChildId
                )
            );
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

    private static AuditLogEntry Audit(
        int hour,
        AuditAction action,
        Guid? actor,
        Guid? user,
        Guid? relatedUser = null
    )
    {
        var occurredAt = Morning.AddHours(hour);
        return new AuditLogEntry
        {
            Id = Guid.CreateVersion7(occurredAt),
            OccurredAt = occurredAt,
            ActorType = actor is null ? "Anonymous" : "User",
            ActorId = actor,
            ClientIp = IPAddress.Parse($"203.0.113.{hour + 1}"),
            Action = action.ToString(),
            EntityType = user is null
                ? nameof(AuditEntityType.Activity)
                : nameof(AuditEntityType.User),
            EntityId = user ?? ActivityId,
            Related = relatedUser is null
                ? null
                : $$"""[{"type": "User", "id": "{{relatedUser}}"}]""",
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
        data.Account.PendingEmail.Should().Be("nueva@codigoactivo.test");
        data.Account.AdultDeclaredAt.Should().Be(SeededAt);
        data.NewsConsentChanges.Should()
            .Equal(new PersonalDataConsentChangeResponse(true, "Registration", SeededAt));
        var dependent = data.Dependents.Should().ContainSingle().Subject;
        dependent.Id.Should().Be(TestSeedData.Users.MemberChildId);
        dependent.BirthDate.Should().Be(new DateOnly(2015, 5, 5));
        dependent.GuardianshipDeclaredAt.Should().Be(SeededAt);
        dependent.UserType.Should().Be("Participante");
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
        data.SecurityLog.Should()
            .Equal(
                new AccountAuditEntryResponse(
                    Morning,
                    AuditAction.Login,
                    AuditOutcome.Succeeded,
                    AuditActorType.Anonymous,
                    false,
                    "203.0.113.1"
                ),
                new AccountAuditEntryResponse(
                    Morning.AddHours(1),
                    AuditAction.ChangePassword,
                    AuditOutcome.Succeeded,
                    AuditActorType.User,
                    true,
                    "203.0.113.2"
                ),
                new AccountAuditEntryResponse(
                    Morning.AddHours(2),
                    AuditAction.ChangeUserType,
                    AuditOutcome.Succeeded,
                    AuditActorType.User,
                    false,
                    null
                ),
                new AccountAuditEntryResponse(
                    Morning.AddHours(3),
                    AuditAction.ChangeAssignmentStatus,
                    AuditOutcome.Succeeded,
                    AuditActorType.User,
                    false,
                    null
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
