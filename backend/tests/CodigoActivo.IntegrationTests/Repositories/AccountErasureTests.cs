using System.Data.Common;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CodigoActivo.IntegrationTests.Repositories;

public sealed class AccountErasureTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string AuthenticatorKey = "protected:authenticator-secret";
    private const string LoginCodeHash = "fake:login-code";
    private const string ResetCodeHash = "fake:reset-code";
    private const string ConditionsText = """
        {"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Acepto las normas"}]}]}
        """;

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid ThumbnailId = new("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid CampId = new("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid ClimbingId = new("dddddddd-0000-0000-0000-000000000003");
    private static readonly Guid WorkshopId = new("dddddddd-0000-0000-0000-000000000004");
    private static readonly Guid ConferenceId = new("dddddddd-0000-0000-0000-000000000005");
    private static readonly Guid TalkId = new("dddddddd-0000-0000-0000-000000000006");
    private static readonly Guid TripId = new("dddddddd-0000-0000-0000-000000000007");
    private static readonly Guid ConditionsId = new("dddddddd-0000-0000-0000-000000000008");
    private static readonly Guid ImageRightsId = new("dddddddd-0000-0000-0000-000000000009");
    private static readonly Guid TripRulesId = new("dddddddd-0000-0000-0000-00000000000a");
    private static readonly Guid LoginChallengeId = new("dddddddd-0000-0000-0000-00000000000b");
    private static readonly Guid ReviewedEventId = new("dddddddd-0000-0000-0000-00000000000c");
    private static readonly Guid AuthoredActivityId = new("dddddddd-0000-0000-0000-00000000000d");
    private static readonly Guid NewsItemId = new("dddddddd-0000-0000-0000-00000000000e");
    private static readonly Guid PartnerId = new("dddddddd-0000-0000-0000-00000000000f");
    private static readonly Guid ResourceId = new("dddddddd-0000-0000-0000-000000000010");
    private static readonly Guid UploadId = new("dddddddd-0000-0000-0000-000000000011");
    private static readonly Guid OtherUploadId = new("dddddddd-0000-0000-0000-000000000012");

    private static readonly DateTimeOffset MemberSignedUpAt = SeededAt.AddDays(1);
    private static readonly DateTimeOffset ChildSignedUpAt = SeededAt.AddDays(2);
    private static readonly DateTimeOffset ConditionsDecidedAt = SeededAt.AddDays(3);
    private static readonly DateTimeOffset ImageRightsDecidedAt = SeededAt.AddDays(4);

    private static Event NewEvent(Guid id, string title, DateOnly startsOn)
    {
        return Persisted.As<Event>(
            new
            {
                Id = id,
                Title = title,
                Subtitle = "Sub",
                Description = "{}",
                EventStartsAt = startsOn,
                EventEndsAt = startsOn.AddDays(1),
                SignupStartsAt = SeededAt,
                SignupEndsAt = SeededAt.AddDays(10),
                ThumbnailId = ThumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            }
        );
    }

    private static Activity NewActivity(
        Guid id,
        Guid eventId,
        string title,
        DateTimeOffset startsAt,
        Guid modalityId
    )
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
                ActivityModalityTypeId = modalityId,
                ThumbnailId = ThumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            }
        );
    }

    private static ActivityUserRoleAssignment NewAssignment(
        Guid userId,
        Guid activityId,
        Guid roleId,
        Guid statusId,
        DateTimeOffset createdAt
    )
    {
        return Persisted.As<ActivityUserRoleAssignment>(
            new
            {
                UserId = userId,
                ActivityId = activityId,
                ActivityRoleTypeId = roleId,
                AssignmentStatusId = statusId,
                CreatedAt = createdAt,
            }
        );
    }

    private static EventTermsAcceptance NewDecision(
        Guid eventId,
        Guid userId,
        Guid documentId,
        bool accepted,
        DateTimeOffset decidedAt
    )
    {
        return Persisted.As<EventTermsAcceptance>(
            new
            {
                EventId = eventId,
                UserId = userId,
                TermsDocumentId = documentId,
                Accepted = accepted,
                DecidedAt = decidedAt,
            }
        );
    }

    private Task SeedHouseholdAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.MemberId, Ct);
            Persisted.Overwrite(
                member,
                new
                {
                    AuthenticatorKey = AuthenticatorKey,
                    TwoFactorMethod = TwoFactorMethod.Authenticator,
                    LoginCodeHash = LoginCodeHash,
                    PasswordResetCodeHash = ResetCodeHash,
                    LoginChallengeId = LoginChallengeId,
                    LastLoginAt = SeededAt.AddDays(5),
                }
            );

            db.Files.Add(
                Persisted.As<FileEntity>(
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
            db.TermsDocuments.AddRange(
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = ConditionsId,
                        Name = "Condiciones",
                        Description = ConditionsText,
                    }
                ),
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = ImageRightsId,
                        Name = "Derechos de imagen",
                        Description = "{}",
                    }
                ),
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = TripRulesId,
                        Name = "Normas de la excursión",
                        Description = "{}",
                    }
                )
            );
            db.Events.AddRange(
                NewEvent(CampId, "Campamento", new DateOnly(2026, 6, 1)),
                NewEvent(ConferenceId, "Jornada", new DateOnly(2026, 8, 1)),
                NewEvent(TripId, "Excursión", new DateOnly(2026, 9, 1))
            );
            db.Activities.AddRange(
                NewActivity(
                    ClimbingId,
                    CampId,
                    "Escalada",
                    new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                    SeedIds.ActivityModalityTypes.Presencial
                ),
                NewActivity(
                    WorkshopId,
                    CampId,
                    "Taller",
                    new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
                    SeedIds.ActivityModalityTypes.Online
                ),
                NewActivity(
                    TalkId,
                    ConferenceId,
                    "Charla",
                    new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                    SeedIds.ActivityModalityTypes.Presencial
                )
            );
            db.ActivityUserRoleAssignments.AddRange(
                NewAssignment(
                    TestSeedData.Users.MemberId,
                    ClimbingId,
                    SeedIds.ActivityRoleTypes.Volunteer,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    MemberSignedUpAt
                ),
                NewAssignment(
                    TestSeedData.Users.MemberChildId,
                    ClimbingId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    ChildSignedUpAt
                ),
                NewAssignment(
                    TestSeedData.Users.MemberChildId,
                    WorkshopId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Requested,
                    ChildSignedUpAt
                ),
                NewAssignment(
                    TestSeedData.Users.BlockedId,
                    TalkId,
                    SeedIds.ActivityRoleTypes.Participant,
                    SeedIds.AssignmentStatusTypes.Confirmed,
                    SeededAt
                )
            );
            db.EventTermsAcceptances.AddRange(
                NewDecision(
                    CampId,
                    TestSeedData.Users.MemberId,
                    ConditionsId,
                    accepted: true,
                    ConditionsDecidedAt
                ),
                NewDecision(
                    CampId,
                    TestSeedData.Users.MemberId,
                    ImageRightsId,
                    accepted: false,
                    ImageRightsDecidedAt
                ),
                NewDecision(
                    TripId,
                    TestSeedData.Users.MemberId,
                    TripRulesId,
                    accepted: true,
                    SeededAt
                ),
                NewDecision(
                    ConferenceId,
                    TestSeedData.Users.BlockedId,
                    ConditionsId,
                    accepted: true,
                    SeededAt
                )
            );
        });
    }

    private Task SeedCopyAlreadyStoredForPendingAsync()
    {
        return Factory.SeedAsync(db =>
        {
            db.News.Add(
                Persisted.As<NewsItem>(
                    new
                    {
                        Id = NewsItemId,
                        Title = "Nota",
                        Subtitle = "Sub",
                        Description = "{}",
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.PendingId,
                    }
                )
            );
            db.DeletedAccounts.Add(
                Persisted.As<DeletedAccount>(
                    new
                    {
                        Id = TestSeedData.Users.PendingId,
                        DeletedAt = SeededAt,
                        Data = "{}",
                    }
                )
            );
            return Task.CompletedTask;
        });
    }

    private async Task AssertPendingKeptWithItsContentAsync()
    {
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().NotBeNull();
        (await FindAsync<NewsItem>(NewsItemId))!
            .CreatedBy.Should()
            .Be(TestSeedData.Users.PendingId, "the handover is rolled back with the deletion");
        (await CopiesAsync())
            .Should()
            .ContainSingle()
            .Which.Data.Should()
            .Be("{}", "only the copy stored before remains");
    }

    private async Task<bool> EraseAsync(
        Guid userId,
        AccountDeletionOrigin origin,
        Guid actorId,
        params Guid[] alsoTracked
    )
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId, Ct);
        await db.Users.Where(u => alsoTracked.Contains(u.Id)).LoadAsync(Ct);
        var eraser = scope.ServiceProvider.GetRequiredService<AccountEraser>();
        return await eraser.EraseAsync(
            user,
            new AccountErasure(origin, actorId, Factory.Clock.UtcNow),
            Ct
        );
    }

    private async Task<bool> EraseThroughAsync(IInterceptor interceptor, Guid userId)
    {
        var connectionString = await Factory.QueryAsync(db =>
            Task.FromResult(db.Database.GetConnectionString()!)
        );
        await using var db = new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(interceptor)
                .Options
        );
        var user = await db.Users.SingleAsync(u => u.Id == userId, Ct);
        var eraser = new AccountEraser(
            new UserRepository(db),
            new DeletedAccountRepository(db),
            new AccountErasureStore(db),
            db
        );
        return await eraser.EraseAsync(
            user,
            new AccountErasure(AccountDeletionOrigin.Self, userId, Factory.Clock.UtcNow),
            Ct
        );
    }

    private Task<List<DeletedAccount>> CopiesAsync()
    {
        return Factory.QueryAsync(db => db.DeletedAccounts.AsNoTracking().ToListAsync(Ct));
    }

    private async Task<JsonElement> CopyOfAsync(Guid userId)
    {
        var copy = (await CopiesAsync()).Single(c => c.Id == userId);
        using var document = JsonDocument.Parse(
            copy.Data,
            new JsonDocumentOptions { MaxDepth = 128 }
        );
        return document.RootElement.Clone();
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
    public async Task EraseAsyncMemberStoresTheWholeHouseholdAndDeletesIt()
    {
        await SeedHouseholdAsync();

        var erased = await EraseAsync(
            TestSeedData.Users.MemberId,
            AccountDeletionOrigin.Self,
            TestSeedData.Users.MemberId
        );

        erased.Should().BeTrue();
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
        (await FindAsync<User>(TestSeedData.Users.MemberChildId)).Should().BeNull();
        (await FindAsync<User>(TestSeedData.Users.BlockedId)).Should().NotBeNull();
        await Factory.QueryAsync(async db =>
        {
            (await db.ActivityUserRoleAssignments.Select(a => a.UserId).ToListAsync(Ct))
                .Should()
                .Equal(TestSeedData.Users.BlockedId);
            (await db.EventTermsAcceptances.Select(a => a.UserId).ToListAsync(Ct))
                .Should()
                .Equal(TestSeedData.Users.BlockedId);
            return true;
        });

        var stored = (await CopiesAsync()).Should().ContainSingle().Subject;
        stored.Id.Should().Be(TestSeedData.Users.MemberId);
        stored.DeletedAt.Should().Be(Factory.Clock.UtcNow);

        var copy = await CopyOfAsync(TestSeedData.Users.MemberId);
        copy.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        copy.GetProperty("deletion").GetProperty("origin").GetString().Should().Be("Self");
        IdOf(copy.GetProperty("deletion"), "actorId").Should().Be(TestSeedData.Users.MemberId);
        copy.GetProperty("guardian").ValueKind.Should().Be(JsonValueKind.Null);

        var account = copy.GetProperty("account");
        IdOf(account).Should().Be(TestSeedData.Users.MemberId);
        account.GetProperty("firstName").GetString().Should().Be("Marta");
        account.GetProperty("lastName").GetString().Should().Be("Miembro");
        account.GetProperty("email").GetString().Should().Be(TestSeedData.MemberEmail);
        account.GetProperty("phone").GetString().Should().Be("+34600000002");
        account.GetProperty("secondaryPhone").ValueKind.Should().Be(JsonValueKind.Null);
        account.GetProperty("nationalId").GetString().Should().Be(TestSeedData.MemberNationalId);
        account.GetProperty("gender").GetString().Should().Be("Female");
        account.GetProperty("promotionalConsent").GetBoolean().Should().BeTrue();
        account.GetProperty("isAdmin").GetBoolean().Should().BeFalse();
        account.GetProperty("twoFactorMethod").GetString().Should().Be("Authenticator");
        account.GetProperty("userTypeName").GetString().Should().Be("Socio");
        account.GetProperty("userStatusTypeName").GetString().Should().Be("Activo");
        account.GetProperty("createdAt").GetDateTimeOffset().Should().Be(SeededAt);
        account.GetProperty("lastLoginAt").GetDateTimeOffset().Should().Be(SeededAt.AddDays(5));
        account.GetProperty("parentId").ValueKind.Should().Be(JsonValueKind.Null);

        var dependent = Items(copy.GetProperty("dependents")).Should().ContainSingle().Subject;
        IdOf(dependent).Should().Be(TestSeedData.Users.MemberChildId);
        dependent.GetProperty("firstName").GetString().Should().Be("Mateo");
        dependent.GetProperty("birthDate").GetString().Should().Be("2015-05-05");
        IdOf(dependent, "parentId").Should().Be(TestSeedData.Users.MemberId);
        dependent.GetProperty("userStatusTypeName").GetString().Should().Be("Dependiente");

        Items(copy.GetProperty("termsDocuments"))
            .Select(document => document.GetProperty("name").GetString())
            .Should()
            .Equal("Condiciones", "Derechos de imagen", "Normas de la excursión");
        var conditions = Items(copy.GetProperty("termsDocuments"))[0];
        IdOf(conditions).Should().Be(ConditionsId);
        conditions
            .GetProperty("textAtDeletion")
            .GetProperty("content")[0]
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString()
            .Should()
            .Be("Acepto las normas");

        var events = Items(copy.GetProperty("events"));
        events.Select(e => IdOf(e)).Should().Equal(CampId, TripId);

        var camp = events[0];
        camp.GetProperty("title").GetString().Should().Be("Campamento");
        camp.GetProperty("startsOn").GetString().Should().Be("2026-06-01");
        camp.GetProperty("endsOn").GetString().Should().Be("2026-06-02");
        var campDecisions = Items(camp.GetProperty("termsDecisions"));
        campDecisions
            .Select(d => IdOf(d, "documentId"))
            .Should()
            .Equal(ConditionsId, ImageRightsId);
        campDecisions[0].GetProperty("accepted").GetBoolean().Should().BeTrue();
        campDecisions[0]
            .GetProperty("decidedAt")
            .GetDateTimeOffset()
            .Should()
            .Be(ConditionsDecidedAt);
        campDecisions[1].GetProperty("documentName").GetString().Should().Be("Derechos de imagen");
        campDecisions[1].GetProperty("accepted").GetBoolean().Should().BeFalse();
        campDecisions
            .Select(d => IdOf(d, "decidedBy"))
            .Should()
            .AllBeEquivalentTo(TestSeedData.Users.MemberId);

        var campActivities = Items(camp.GetProperty("activities"));
        campActivities
            .Select(a => (IdOf(a), IdOf(a, "participantId"), a.GetProperty("status").GetString()))
            .Should()
            .Equal(
                (ClimbingId, TestSeedData.Users.MemberId, "Confirmada"),
                (ClimbingId, TestSeedData.Users.MemberChildId, "Confirmada"),
                (WorkshopId, TestSeedData.Users.MemberChildId, "Solicitada")
            );
        var volunteering = campActivities[0];
        volunteering.GetProperty("title").GetString().Should().Be("Escalada");
        volunteering.GetProperty("location").GetString().Should().Be("Sala Escalada");
        volunteering.GetProperty("modality").GetString().Should().Be("Presencial");
        volunteering.GetProperty("role").GetString().Should().Be("Voluntario");
        volunteering.GetProperty("signedUpAt").GetDateTimeOffset().Should().Be(MemberSignedUpAt);
        volunteering
            .GetProperty("startsAt")
            .GetDateTimeOffset()
            .Should()
            .Be(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero));
        campActivities[2].GetProperty("modality").GetString().Should().Be("Online");

        var trip = events[1];
        Items(trip.GetProperty("termsDecisions"))
            .Select(d => IdOf(d, "documentId"))
            .Should()
            .Equal(TripRulesId);
        Items(trip.GetProperty("activities")).Should().BeEmpty();
    }

    [Fact]
    public async Task EraseAsyncNeverStoresCredentialsCodesOrSecrets()
    {
        await SeedHouseholdAsync();

        await EraseAsync(
            TestSeedData.Users.MemberId,
            AccountDeletionOrigin.Self,
            TestSeedData.Users.MemberId
        );

        var data = (await CopiesAsync()).Single().Data;
        data.Should().NotContain(TestSeedData.PasswordHash);
        data.Should().NotContain(AuthenticatorKey);
        data.Should().NotContain(LoginCodeHash);
        data.Should().NotContain(ResetCodeHash);
        data.Should().NotContain(LoginChallengeId.ToString());
        var account = (await CopyOfAsync(TestSeedData.Users.MemberId)).GetProperty("account");
        foreach (
            var key in new[]
            {
                "passwordHash",
                "authenticatorKey",
                "pendingAuthenticatorKey",
                "loginCodeHash",
                "passwordResetCodeHash",
                "otpCodeHash",
                "loginChallengeId",
                "passwordFailedAttempts",
                "twoFactorFailedAttempts",
            }
        )
        {
            account.TryGetProperty(key, out _).Should().BeFalse(key);
        }
    }

    [Fact]
    public async Task EraseAsyncDependentStoresTheGuardianAndTheirDecisionsForItsEventsOnly()
    {
        await SeedHouseholdAsync();

        var erased = await EraseAsync(
            TestSeedData.Users.MemberChildId,
            AccountDeletionOrigin.Guardian,
            TestSeedData.Users.MemberId
        );

        erased.Should().BeTrue();
        (await FindAsync<User>(TestSeedData.Users.MemberChildId)).Should().BeNull();
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().NotBeNull();
        await Factory.QueryAsync(async db =>
        {
            (await db.ActivityUserRoleAssignments.CountAsync(Ct)).Should().Be(2);
            (await db.EventTermsAcceptances.CountAsync(Ct))
                .Should()
                .Be(4, "the guardian's decisions are theirs and stay");
            return true;
        });

        var copy = await CopyOfAsync(TestSeedData.Users.MemberChildId);
        copy.GetProperty("deletion").GetProperty("origin").GetString().Should().Be("Guardian");
        IdOf(copy.GetProperty("deletion"), "actorId").Should().Be(TestSeedData.Users.MemberId);
        IdOf(copy.GetProperty("account")).Should().Be(TestSeedData.Users.MemberChildId);
        Items(copy.GetProperty("dependents")).Should().BeEmpty();

        var guardian = copy.GetProperty("guardian");
        IdOf(guardian).Should().Be(TestSeedData.Users.MemberId);
        guardian.GetProperty("firstName").GetString().Should().Be("Marta");
        guardian.GetProperty("nationalId").GetString().Should().Be(TestSeedData.MemberNationalId);
        guardian.GetProperty("email").GetString().Should().Be(TestSeedData.MemberEmail);
        guardian.GetProperty("phone").GetString().Should().Be("+34600000002");

        var camp = Items(copy.GetProperty("events")).Should().ContainSingle().Subject;
        IdOf(camp).Should().Be(CampId);
        Items(camp.GetProperty("termsDecisions"))
            .Select(d => (IdOf(d, "documentId"), IdOf(d, "decidedBy")))
            .Should()
            .Equal(
                (ConditionsId, TestSeedData.Users.MemberId),
                (ImageRightsId, TestSeedData.Users.MemberId)
            );
        Items(camp.GetProperty("activities"))
            .Select(a => IdOf(a, "participantId"))
            .Should()
            .AllBeEquivalentTo(TestSeedData.Users.MemberChildId);
        Items(copy.GetProperty("termsDocuments"))
            .Select(document => IdOf(document))
            .Should()
            .BeEquivalentTo([ConditionsId, ImageRightsId]);
    }

    [Fact]
    public async Task EraseAsyncAccountWithoutParticipationStoresEmptyCollections()
    {
        var erased = await EraseAsync(
            TestSeedData.Users.PendingId,
            AccountDeletionOrigin.Administrator,
            TestSeedData.Users.AdminId
        );

        erased.Should().BeTrue();
        var copy = await CopyOfAsync(TestSeedData.Users.PendingId);
        copy.GetProperty("deletion").GetProperty("origin").GetString().Should().Be("Administrator");
        IdOf(copy.GetProperty("deletion"), "actorId").Should().Be(TestSeedData.Users.AdminId);
        copy.GetProperty("account")
            .GetProperty("userStatusTypeName")
            .GetString()
            .Should()
            .Be("Pendiente");
        Items(copy.GetProperty("dependents")).Should().BeEmpty();
        Items(copy.GetProperty("termsDocuments")).Should().BeEmpty();
        Items(copy.GetProperty("events")).Should().BeEmpty();
    }

    [Fact]
    public async Task EraseAsyncWithTheDependentsTrackedStillStoresOneCopy()
    {
        await SeedHouseholdAsync();

        var erased = await EraseAsync(
            TestSeedData.Users.MemberId,
            AccountDeletionOrigin.Self,
            TestSeedData.Users.MemberId,
            TestSeedData.Users.MemberChildId
        );

        erased.Should().BeTrue();
        (await CopiesAsync()).Select(c => c.Id).Should().Equal(TestSeedData.Users.MemberId);
        (await FindAsync<User>(TestSeedData.Users.MemberChildId)).Should().BeNull();
    }

    [Fact]
    public async Task EraseAsyncUserErasedMeanwhileReturnsFalseAndStoresNothingMore()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var stale = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.PendingId, Ct);
        await EraseAsync(
            TestSeedData.Users.PendingId,
            AccountDeletionOrigin.Administrator,
            TestSeedData.Users.AdminId
        );

        var erased = await scope
            .ServiceProvider.GetRequiredService<AccountEraser>()
            .EraseAsync(
                stale,
                new AccountErasure(
                    AccountDeletionOrigin.Administrator,
                    TestSeedData.Users.AdminId,
                    Factory.Clock.UtcNow
                ),
                Ct
            );

        erased.Should().BeFalse();
        (await CopiesAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task EraseAsyncRefusedByTheDatabaseKeepsTheUserAndRollsBackTheHandover()
    {
        await SeedHouseholdAsync();
        await SeedCopyAlreadyStoredForPendingAsync();

        var erase = () =>
            EraseAsync(
                TestSeedData.Users.PendingId,
                AccountDeletionOrigin.Administrator,
                TestSeedData.Users.AdminId
            );

        await erase.Should().ThrowAsync<UniqueConstraintViolationException>();
        await AssertPendingKeptWithItsContentAsync();
    }

    [Fact]
    public async Task EraseAsyncHandsEveryCreditOfTheHouseholdOverToTheInitialAdministrator()
    {
        await SeedHouseholdAsync();
        var member = TestSeedData.Users.MemberId;
        var child = TestSeedData.Users.MemberChildId;
        var other = TestSeedData.Users.PendingId;
        await Factory.SeedAsync(db =>
        {
            var reviewed = NewEvent(ReviewedEventId, "Revisado", new DateOnly(2026, 10, 1));
            Persisted.Overwrite(reviewed, new { CreatedBy = other, UpdatedBy = child });
            var authored = NewActivity(
                AuthoredActivityId,
                CampId,
                "Autoría",
                new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero),
                SeedIds.ActivityModalityTypes.Online
            );
            Persisted.Overwrite(authored, new { CreatedBy = child, UpdatedBy = member });
            db.Events.Add(reviewed);
            db.Activities.Add(authored);
            db.News.Add(
                Persisted.As<NewsItem>(
                    new
                    {
                        Id = NewsItemId,
                        Title = "Nota",
                        Subtitle = "Sub",
                        Description = "{}",
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = member,
                        UpdatedBy = other,
                    }
                )
            );
            db.Partners.Add(
                Persisted.As<Partner>(
                    new
                    {
                        Id = PartnerId,
                        Name = "Colaborador",
                        Tier = 1,
                        FromDate = new DateOnly(2024, 1, 1),
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = member,
                    }
                )
            );
            db.Resources.Add(
                Persisted.As<Resource>(
                    new
                    {
                        Id = ResourceId,
                        Title = "Recurso",
                        Subtitle = "Sub",
                        Description = "{}",
                        ResourceTypeId = SeedIds.ResourceTypes.Internal,
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = other,
                        UpdatedBy = member,
                    }
                )
            );
            db.Files.AddRange(
                Persisted.As<FileEntity>(
                    new
                    {
                        Id = UploadId,
                        Name = "subida",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = child,
                    }
                ),
                Persisted.As<FileEntity>(
                    new
                    {
                        Id = OtherUploadId,
                        Name = "ajena",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = other,
                    }
                )
            );
            return Task.CompletedTask;
        });

        var erased = await EraseAsync(member, AccountDeletionOrigin.Self, member);

        erased.Should().BeTrue();
        var heir = SeedIds.Users.InitialAdministrator;
        var reviewedEvent = (await FindAsync<Event>(ReviewedEventId))!;
        reviewedEvent.CreatedBy.Should().Be(other);
        reviewedEvent.UpdatedBy.Should().Be(heir);
        var authoredActivity = (await FindAsync<Activity>(AuthoredActivityId))!;
        authoredActivity.CreatedBy.Should().Be(heir);
        authoredActivity.UpdatedBy.Should().Be(heir);
        var newsItem = (await FindAsync<NewsItem>(NewsItemId))!;
        newsItem.CreatedBy.Should().Be(heir);
        newsItem.UpdatedBy.Should().Be(other);
        (await FindAsync<Partner>(PartnerId))!.CreatedBy.Should().Be(heir);
        var resource = (await FindAsync<Resource>(ResourceId))!;
        resource.CreatedBy.Should().Be(other);
        resource.UpdatedBy.Should().Be(heir);
        (await FindAsync<FileEntity>(UploadId))!.UploadedBy.Should().Be(heir);
        (await FindAsync<FileEntity>(OtherUploadId))!.UploadedBy.Should().Be(other);
        (await FindAsync<User>(member)).Should().BeNull();
        (await FindAsync<User>(child)).Should().BeNull();
    }

    [Fact]
    public async Task EraseAsyncInitialAdministratorIsRefusedAndKeepsEverything()
    {
        await SeedHouseholdAsync();
        var administrator = SeedIds.Users.InitialAdministrator;

        var erase = () => EraseAsync(administrator, AccountDeletionOrigin.Self, administrator);

        await erase
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*initial administrator*");
        (await FindAsync<User>(administrator)).Should().NotBeNull();
        (await FindAsync<Event>(CampId))!.CreatedBy.Should().Be(administrator);
        (await CopiesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesRemovingAUserWithoutErasingIsRefusedAndKeepsTheUser()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var user = await users.GetByIdAsync(TestSeedData.Users.PendingId, Ct);
        users.Remove(user!);

        var save = () => uow.SaveChangesAsync(Ct);

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EraseAsync*");
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeAsyncDeletesCopiesUpToTheInstantAndKeepsLaterOnes()
    {
        var cutoff = Factory.Clock.UtcNow.AddYears(-2);
        var before = Guid.NewGuid();
        var exactly = Guid.NewGuid();
        var after = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.DeletedAccounts.AddRange(
                Persisted.As<DeletedAccount>(
                    new { Id = before, DeletedAt = cutoff.AddSeconds(-1) }
                ),
                Persisted.As<DeletedAccount>(new { Id = exactly, DeletedAt = cutoff }),
                Persisted.As<DeletedAccount>(new { Id = after, DeletedAt = cutoff.AddSeconds(1) })
            );
            return Task.CompletedTask;
        });

        var purged = await Factory.QueryAsync(db =>
            new DeletedAccountRepository(db).RemoveDeletedUpToAsync(cutoff, Ct)
        );

        purged.Should().Be(2);
        (await CopiesAsync()).Select(c => c.Id).Should().Equal(after);
    }

    [Fact]
    public async Task EraseAsyncLocksEveryHouseholdRowItCopiesBeforeReadingIt()
    {
        await SeedHouseholdAsync();
        var probe = new LockProbe(
            await Factory.QueryAsync(db => Task.FromResult(db.Database.GetConnectionString()!)),
            [
                (LockedRows.User, TestSeedData.Users.MemberId),
                (LockedRows.User, TestSeedData.Users.MemberChildId),
                (LockedRows.Assignments, TestSeedData.Users.MemberChildId),
                (LockedRows.Decisions, TestSeedData.Users.MemberId),
                (LockedRows.User, TestSeedData.Users.BlockedId),
                (LockedRows.Assignments, TestSeedData.Users.BlockedId),
            ]
        );

        var erased = await EraseThroughAsync(probe, TestSeedData.Users.MemberId);

        erased.Should().BeTrue();
        probe.Probed.Should().BeTrue();
        probe
            .Blocked.Should()
            .Equal(
                TestSeedData.Users.MemberId,
                TestSeedData.Users.MemberChildId,
                TestSeedData.Users.MemberChildId,
                TestSeedData.Users.MemberId
            );
    }

    [Fact]
    public async Task EraseAsyncAbortedByDeadlocksIsRetriedAndStoresOneCopy()
    {
        await SeedHouseholdAsync();
        var deadlocks = new DeadlockInterceptor("FOR UPDATE", deadlocks: 2);

        var erased = await EraseThroughAsync(deadlocks, TestSeedData.Users.MemberId);

        erased.Should().BeTrue();
        deadlocks.Thrown.Should().Be(2);
        (await CopiesAsync()).Select(c => c.Id).Should().Equal(TestSeedData.Users.MemberId);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
    }

    [Fact]
    public async Task EraseAsyncAbortedByADeadlockWhileSavingIsRetriedAndStoresOneCopy()
    {
        await SeedHouseholdAsync();
        var deadlocks = new DeadlockInterceptor("DELETE FROM", deadlocks: 1);

        var erased = await EraseThroughAsync(deadlocks, TestSeedData.Users.MemberId);

        erased.Should().BeTrue();
        deadlocks.Thrown.Should().Be(1);
        (await CopiesAsync()).Select(c => c.Id).Should().Equal(TestSeedData.Users.MemberId);
        (await FindAsync<User>(TestSeedData.Users.MemberChildId)).Should().BeNull();
    }

    [Fact]
    public async Task EraseAsyncDeadlockedOnEveryAttemptGivesUpAndKeepsEverything()
    {
        await SeedHouseholdAsync();
        var deadlocks = new DeadlockInterceptor("FOR UPDATE", deadlocks: int.MaxValue);

        var erase = () => EraseThroughAsync(deadlocks, TestSeedData.Users.MemberId);

        (await erase.Should().ThrowAsync<InvalidOperationException>())
            .Which.GetBaseException()
            .Should()
            .BeOfType<PostgresException>()
            .Which.SqlState.Should()
            .Be(PostgresErrorCodes.DeadlockDetected);
        deadlocks.Thrown.Should().Be(3);
        (await CopiesAsync()).Should().BeEmpty();
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().NotBeNull();
    }

    [Fact]
    public async Task EraseAsyncRefusedByTheDatabaseLeavesNoCopyStagedForALaterCommit()
    {
        await SeedHouseholdAsync();
        await SeedCopyAlreadyStoredForPendingAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var eraser = scope.ServiceProvider.GetRequiredService<AccountEraser>();
        var user = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.PendingId, Ct);
        var erasure = new AccountErasure(
            AccountDeletionOrigin.Administrator,
            TestSeedData.Users.AdminId,
            Factory.Clock.UtcNow
        );

        var erase = () => eraser.EraseAsync(user, erasure, Ct);
        var save = () =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        await erase.Should().ThrowAsync<UniqueConstraintViolationException>();
        await erase
            .Should()
            .ThrowAsync<UniqueConstraintViolationException>("a retry stages a fresh copy");
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EraseAsync*");
        await AssertPendingKeptWithItsContentAsync();
    }

    [Fact]
    public async Task EraseAsyncCommitsOtherStagedChangesThroughTheUnitOfWork()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.PendingId, Ct);
        db.Users.Add(
            Persisted.As<User>(
                new
                {
                    FirstName = "Duplicado",
                    LastName = "Duplicado",
                    Email = TestSeedData.MemberEmail,
                    Gender = Gender.Other,
                    UserStatusTypeId = SeedIds.UserStatusTypes.Pending,
                    UserTypeId = SeedIds.UserTypes.Member,
                    CreatedAt = SeededAt,
                }
            )
        );

        var erase = () =>
            scope
                .ServiceProvider.GetRequiredService<AccountEraser>()
                .EraseAsync(
                    user,
                    new AccountErasure(
                        AccountDeletionOrigin.Administrator,
                        TestSeedData.Users.AdminId,
                        Factory.Clock.UtcNow
                    ),
                    Ct
                );

        await erase.Should().ThrowAsync<UniqueConstraintViolationException>();
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().NotBeNull();
        (await CopiesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EraseAsyncCopiesTermsDocumentsNestedAsDeepAsValidationAllows()
    {
        await SeedHouseholdAsync();
        const int ValidatedMaxDepth = 64;
        await Factory.SeedAsync(async db =>
        {
            var conditions = await db.TermsDocuments.SingleAsync(d => d.Id == ConditionsId, Ct);
            conditions.Rewrite(
                conditions.Name,
                new string('[', ValidatedMaxDepth) + new string(']', ValidatedMaxDepth)
            );
        });

        var erased = await EraseAsync(
            TestSeedData.Users.MemberId,
            AccountDeletionOrigin.Self,
            TestSeedData.Users.MemberId
        );

        erased.Should().BeTrue();
        var text = Items(
                (await CopyOfAsync(TestSeedData.Users.MemberId)).GetProperty("termsDocuments")
            )
            .Single(document => IdOf(document) == ConditionsId)
            .GetProperty("textAtDeletion");
        var depth = 0;
        while (text.ValueKind is JsonValueKind.Array)
        {
            depth++;
            text = text.GetArrayLength() is 0 ? default : text[0];
        }

        depth.Should().Be(ValidatedMaxDepth);
    }

    private sealed class DeadlockInterceptor(string trigger, int deadlocks) : DbCommandInterceptor
    {
        public int Thrown { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                Thrown < deadlocks
                && command.CommandText.Contains(trigger, StringComparison.Ordinal)
            )
            {
                Thrown++;
                throw new PostgresException(
                    "deadlock detected",
                    "ERROR",
                    "ERROR",
                    PostgresErrorCodes.DeadlockDetected
                );
            }

            return ValueTask.FromResult(result);
        }
    }

    private enum LockedRows
    {
        User,
        Assignments,
        Decisions,
    }

    private sealed class LockProbe(
        string connectionString,
        IReadOnlyList<(LockedRows Rows, Guid Id)> probes
    ) : DbCommandInterceptor
    {
        private bool locking;

        public bool Probed { get; private set; }

        public List<Guid> Blocked { get; } = [];

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                locking = true;
            }
            else if (locking && !Probed)
            {
                Probed = true;
                await ProbeAsync(cancellationToken);
            }

            return result;
        }

        private async Task ProbeAsync(CancellationToken ct)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            foreach (var (rows, id) in probes)
            {
                await using var transaction = await connection.BeginTransactionAsync(ct);
                await using var probe = LockCommand(rows, connection, transaction);
                probe.Parameters.AddWithValue("id", id);
                try
                {
                    await probe.ExecuteNonQueryAsync(ct);
                }
                catch (PostgresException ex)
                    when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
                {
                    Blocked.Add(id);
                }
            }
        }

        private static NpgsqlCommand LockCommand(
            LockedRows rows,
            NpgsqlConnection connection,
            NpgsqlTransaction transaction
        )
        {
            return rows switch
            {
                LockedRows.User => new NpgsqlCommand(
                    "SELECT 1 FROM users WHERE id = @id FOR UPDATE NOWAIT",
                    connection,
                    transaction
                ),
                LockedRows.Assignments => new NpgsqlCommand(
                    "SELECT 1 FROM activity_user_role_assignments WHERE user_id = @id FOR UPDATE NOWAIT",
                    connection,
                    transaction
                ),
                _ => new NpgsqlCommand(
                    "SELECT 1 FROM event_terms_acceptances WHERE user_id = @id FOR UPDATE NOWAIT",
                    connection,
                    transaction
                ),
            };
        }
    }
}
