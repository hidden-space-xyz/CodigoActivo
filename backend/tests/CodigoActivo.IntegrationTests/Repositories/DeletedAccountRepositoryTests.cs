using System.Data.Common;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CodigoActivo.IntegrationTests.Repositories;

public sealed class DeletedAccountRepositoryTests(CodigoActivoWebAppFactory factory)
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

    private static readonly DateTimeOffset MemberSignedUpAt = SeededAt.AddDays(1);
    private static readonly DateTimeOffset ChildSignedUpAt = SeededAt.AddDays(2);
    private static readonly DateTimeOffset ConditionsDecidedAt = SeededAt.AddDays(3);
    private static readonly DateTimeOffset ImageRightsDecidedAt = SeededAt.AddDays(4);

    private static Event NewEvent(Guid id, string title, DateOnly startsOn)
    {
        return new Event
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
        };
    }

    private static Activity NewActivity(
        Guid id,
        Guid eventId,
        string title,
        DateTimeOffset startsAt,
        Guid modalityId
    )
    {
        return new Activity
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
        };
    }

    private static ActivityUserRoleAssignment NewAssignment(
        Guid userId,
        Guid activityId,
        Guid roleId,
        Guid statusId,
        DateTimeOffset createdAt
    )
    {
        return new ActivityUserRoleAssignment
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = roleId,
            AssignmentStatusId = statusId,
            CreatedAt = createdAt,
        };
    }

    private static EventTermsAcceptance NewDecision(
        Guid eventId,
        Guid userId,
        Guid documentId,
        bool accepted,
        DateTimeOffset decidedAt
    )
    {
        return new EventTermsAcceptance
        {
            EventId = eventId,
            UserId = userId,
            TermsDocumentId = documentId,
            Accepted = accepted,
            DecidedAt = decidedAt,
        };
    }

    private Task SeedHouseholdAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.MemberId, Ct);
            member.AuthenticatorKey = AuthenticatorKey;
            member.TwoFactorMethod = TwoFactorMethod.Authenticator;
            member.LoginCodeHash = LoginCodeHash;
            member.PasswordResetCodeHash = ResetCodeHash;
            member.LoginChallengeId = LoginChallengeId;
            member.LastLoginAt = SeededAt.AddDays(5);

            db.Files.Add(
                new FileEntity
                {
                    Id = ThumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = SeededAt,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            db.TermsDocuments.AddRange(
                new TermsDocument
                {
                    Id = ConditionsId,
                    Name = "Condiciones",
                    Description = ConditionsText,
                },
                new TermsDocument
                {
                    Id = ImageRightsId,
                    Name = "Derechos de imagen",
                    Description = "{}",
                },
                new TermsDocument
                {
                    Id = TripRulesId,
                    Name = "Normas de la excursión",
                    Description = "{}",
                }
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
        var deletedAccounts = scope.ServiceProvider.GetRequiredService<IDeletedAccountRepository>();
        return await deletedAccounts.EraseAsync(
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
        return await new DeletedAccountRepository(
            db,
            NullLogger<DeletedAccountRepository>.Instance
        ).EraseAsync(
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
            .ServiceProvider.GetRequiredService<IDeletedAccountRepository>()
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
    public async Task EraseAsyncRefusedByTheDatabaseKeepsTheUserAndStoresNoCopy()
    {
        await SeedHouseholdAsync();
        await Factory.SeedAsync(db =>
        {
            db.News.Add(
                new NewsItem
                {
                    Title = "Nota",
                    Subtitle = "Sub",
                    Description = "{}",
                    ThumbnailId = ThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.PendingId,
                }
            );
            return Task.CompletedTask;
        });

        var erase = () =>
            EraseAsync(
                TestSeedData.Users.PendingId,
                AccountDeletionOrigin.Administrator,
                TestSeedData.Users.AdminId
            );

        await erase.Should().ThrowAsync<DbUpdateException>();
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().NotBeNull();
        (await CopiesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesRemovingAUserWithoutErasingIsRefusedAndKeepsTheUser()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var user = await users.FindAsync(u => u.Id == TestSeedData.Users.PendingId, Ct);
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
                new DeletedAccount { Id = before, DeletedAt = cutoff.AddSeconds(-1) },
                new DeletedAccount { Id = exactly, DeletedAt = cutoff },
                new DeletedAccount { Id = after, DeletedAt = cutoff.AddSeconds(1) }
            );
            return Task.CompletedTask;
        });

        var purged = await Factory.QueryAsync(db =>
            new DeletedAccountRepository(
                db,
                NullLogger<DeletedAccountRepository>.Instance
            ).PurgeAsync(cutoff, Ct)
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
        await Factory.SeedAsync(db =>
        {
            db.News.Add(
                new NewsItem
                {
                    Title = "Nota",
                    Subtitle = "Sub",
                    Description = "{}",
                    ThumbnailId = ThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.PendingId,
                }
            );
            return Task.CompletedTask;
        });
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var deletedAccounts = scope.ServiceProvider.GetRequiredService<IDeletedAccountRepository>();
        var user = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.PendingId, Ct);
        var erasure = new AccountErasure(
            AccountDeletionOrigin.Administrator,
            TestSeedData.Users.AdminId,
            Factory.Clock.UtcNow
        );

        var erase = () => deletedAccounts.EraseAsync(user, erasure, Ct);
        var save = () =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);

        await erase.Should().ThrowAsync<DbUpdateException>();
        await erase.Should().ThrowAsync<DbUpdateException>("a retry stages a fresh copy");
        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EraseAsync*");
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().NotBeNull();
        (await CopiesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EraseAsyncCommitsOtherStagedChangesThroughTheUnitOfWork()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == TestSeedData.Users.PendingId, Ct);
        db.Users.Add(
            new User
            {
                FirstName = "Duplicado",
                LastName = "Duplicado",
                Email = TestSeedData.MemberEmail,
                Gender = Gender.Other,
                UserStatusTypeId = SeedIds.UserStatusTypes.Pending,
                UserTypeId = SeedIds.UserTypes.Member,
                CreatedAt = SeededAt,
            }
        );

        var erase = () =>
            scope
                .ServiceProvider.GetRequiredService<IDeletedAccountRepository>()
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
            conditions.Description =
                new string('[', ValidatedMaxDepth) + new string(']', ValidatedMaxDepth);
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
