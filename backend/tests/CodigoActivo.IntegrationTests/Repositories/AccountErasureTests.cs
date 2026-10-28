using System.Data.Common;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.Infrastructure.Users;
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
    private static readonly DateTimeOffset ImageRightsAcceptedAt = SeededAt.AddDays(2);
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
                Modality = CatalogIds.ActivityModalities.ValueOf(modalityId),
                ThumbnailId = ThumbnailId,
                CreatedAt = SeededAt,
                CreatedBy = TestSeedData.Users.AdminId,
            }
        );
    }

    private static Assignment NewAssignment(
        Guid userId,
        Guid activityId,
        Guid roleId,
        Guid statusId,
        DateTimeOffset createdAt
    )
    {
        return Persisted.As<Assignment>(
            new
            {
                UserId = userId,
                ActivityId = activityId,
                Role = CatalogIds.ActivityRoles.ValueOf(roleId),
                Status = CatalogIds.AssignmentStatuses.ValueOf(statusId),
                CreatedAt = createdAt,
            }
        );
    }

    private static EventTermsAcceptance NewDecision(
        Guid eventId,
        Guid userId,
        Guid documentId,
        bool accepted,
        DateTimeOffset decidedAt,
        HandwrittenSignature? signature = null,
        Guid? decidedById = null
    )
    {
        var decider = UserId.From(decidedById ?? userId);
        return EventTermsAcceptance.Record(
            EventId.From(eventId),
            UserId.From(userId),
            decider,
            TermsDocumentId.From(documentId),
            1,
            accepted,
            signature,
            decider,
            decidedAt
        );
    }

    private static TermsDocument ImageRightsRewritten()
    {
        var document = TermsDocument.Create(
            "Derechos de imagen",
            RichText.From("{}"),
            false,
            TermsDocumentKind.Conditions,
            TermsDocumentId.From(ImageRightsId)
        );
        document.Rewrite(
            "Derechos de imagen",
            RichText.From(ConditionsText),
            false,
            TermsDocumentKind.Conditions
        );
        return document;
    }

    private static EventTermsAcceptance ImageRightsWithdrawn()
    {
        var decision = NewDecision(
            CampId,
            TestSeedData.Users.MemberId,
            ImageRightsId,
            accepted: true,
            ImageRightsAcceptedAt
        );
        decision.Withdraw(
            UserId.From(TestSeedData.Users.MemberId),
            UserId.From(TestSeedData.Users.AdminId),
            ImageRightsDecidedAt
        );
        return decision;
    }

    private static NewsConsentChange ConsentChange(
        bool granted,
        NewsConsentSource source,
        DateTimeOffset changedAt
    )
    {
        return Persisted.As<NewsConsentChange>(
            new
            {
                UserId = UserId.From(TestSeedData.Users.MemberId),
                Granted = granted,
                Source = source,
                ChangedAt = changedAt,
            }
        );
    }

    private Task SeedHouseholdAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.SingleAsync(
                u => u.Id == UserId.From(TestSeedData.Users.MemberId),
                Ct
            );
            Persisted.Overwrite(
                member,
                new
                {
                    Authenticator = new { Key = AuthenticatorKey },
                    TwoFactorMethod = TwoFactorMethod.Authenticator,
                    LoginCode = new { Hash = LoginCodeHash },
                    PasswordResetCode = new { Hash = ResetCodeHash },
                    LoginChallenge = new { Id = LoginChallengeId },
                    LastLoginAt = SeededAt.AddDays(5),
                }
            );
            db.NewsConsentChanges.AddRange(
                ConsentChange(true, NewsConsentSource.Registration, SeededAt),
                ConsentChange(false, NewsConsentSource.UnsubscribeLink, SeededAt.AddDays(2)),
                ConsentChange(true, NewsConsentSource.Profile, SeededAt.AddDays(3))
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
            db.TermsDocuments.AddRange(
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = ConditionsId,
                        Name = "Condiciones",
                        Description = ConditionsText,
                    }
                ),
                ImageRightsRewritten(),
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
                    KnownIds.ActivityModalityTypes.Presencial
                ),
                NewActivity(
                    WorkshopId,
                    CampId,
                    "Taller",
                    new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
                    KnownIds.ActivityModalityTypes.Online
                ),
                NewActivity(
                    TalkId,
                    ConferenceId,
                    "Charla",
                    new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                    KnownIds.ActivityModalityTypes.Presencial
                )
            );
            db.Assignments.AddRange(
                NewAssignment(
                    TestSeedData.Users.MemberId,
                    ClimbingId,
                    KnownIds.ActivityRoleTypes.Volunteer,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    MemberSignedUpAt
                ),
                NewAssignment(
                    TestSeedData.Users.MemberChildId,
                    ClimbingId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    ChildSignedUpAt
                ),
                NewAssignment(
                    TestSeedData.Users.MemberChildId,
                    WorkshopId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Requested,
                    ChildSignedUpAt
                ),
                NewAssignment(
                    TestSeedData.Users.BlockedId,
                    TalkId,
                    KnownIds.ActivityRoleTypes.Participant,
                    KnownIds.AssignmentStatusTypes.Confirmed,
                    SeededAt
                )
            );
            db.EventTermsAcceptances.AddRange(
                NewDecision(
                    CampId,
                    TestSeedData.Users.MemberId,
                    ConditionsId,
                    accepted: true,
                    ConditionsDecidedAt,
                    HandwrittenSignature.FromStored(TestSeedData.ValidPng())
                ),
                ImageRightsWithdrawn(),
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
            db.EventRatings.AddRange(
                EventRating
                    .Submit(
                        EventId.From(CampId),
                        UserId.From(TestSeedData.Users.MemberId),
                        4,
                        "La escalada",
                        null,
                        "Más sombra"
                    )
                    .Value,
                EventRating
                    .Submit(
                        EventId.From(ConferenceId),
                        UserId.From(TestSeedData.Users.BlockedId),
                        2,
                        null,
                        null,
                        null
                    )
                    .Value
            );
        });
    }

    private Task<int> ForgetSeededHistoryAsync()
    {
        return Factory.QueryAsync(db => db.DataHistory.ExecuteDeleteAsync(Ct));
    }

    private async Task<bool> EraseAsync(Guid userId, params Guid[] alsoTracked)
    {
        await ForgetSeededHistoryAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == UserId.From(userId), Ct);
        var tracked = alsoTracked.Select(UserId.From).ToList();
        await db.Users.Where(u => tracked.Contains(u.Id)).LoadAsync(Ct);
        var eraser = scope.ServiceProvider.GetRequiredService<AccountEraser>();
        return await eraser.EraseAsync(user, Ct);
    }

    private async Task<bool> EraseThroughAsync(IInterceptor interceptor, Guid userId)
    {
        await ForgetSeededHistoryAsync();
        var connectionString = await Factory.QueryAsync(db =>
            Task.FromResult(db.Database.GetConnectionString()!)
        );
        await using var scope = Factory.Services.CreateAsyncScope();
        await using var db = new CodigoActivoDbContext(
            new DbContextOptionsBuilder<CodigoActivoDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    interceptor,
                    scope.ServiceProvider.GetRequiredService<DataHistoryRecorder>()
                )
                .Options
        );
        var user = await db.Users.SingleAsync(u => u.Id == UserId.From(userId), Ct);
        var uow = TestUnitOfWork.For(db);
        var eraser = new AccountEraser(
            new UserRepository(db, uow),
            new AccountErasureStore(db),
            scope.ServiceProvider.GetRequiredService<ConsentWithdrawalNotifier>(),
            uow
        );
        return await eraser.EraseAsync(user, Ct);
    }

    private async Task<List<(DataHistoryEntry Entry, DataHistoryRecord Record)>> HistoryAsync()
    {
        var protector = Factory.Services.GetRequiredService<DataHistoryProtector>();
        var entries = await Factory.QueryAsync(db =>
            db.DataHistory.AsNoTracking().OrderBy(entry => entry.Id).ToListAsync(Ct)
        );
        return [.. entries.Select(entry => (entry, protector.Unprotect(entry.Id, entry.Payload)))];
    }

    private async Task<List<DataHistoryRecord>> DeletedAsync<TEntity>()
    {
        return
        [
            .. (await HistoryAsync())
                .Where(item =>
                    item.Entry.Entity == typeof(TEntity).Name
                    && item.Record.Operation is DataHistoryOperation.Deleted
                )
                .Select(item => item.Record),
        ];
    }

    private static string? Value(DataHistoryRecord record, string property)
    {
        return record.Values.TryGetValue(property, out var value) && value is JsonElement element
            ? element.ValueKind is JsonValueKind.String
                ? element.GetString()
                : element.ToString()
            : null;
    }

    private static Guid? GuidValue(DataHistoryRecord record, string property)
    {
        return Guid.TryParse(Value(record, property), out var id) ? id : null;
    }

    [Fact]
    public async Task EraseAsyncDiscardsTheWaitingEmailButNotTheNoticesToTheLegalContact()
    {
        var outbox = Factory.Services.GetRequiredService<IEmailOutboxStore>();
        EmailRecipient[] member = [new(TestSeedData.MemberEmail, "Miembro")];
        EmailKind[] kinds =
        [
            EmailKind.ActivityNotification,
            EmailKind.ConsentWithdrawalNotice,
            EmailKind.BlockedDataConsultationNotice,
        ];
        foreach (var kind in kinds)
        {
            await outbox.TryEnqueueAsync(
                new EmailBatch(kind, "Asunto", "<p>Hola</p>", "Hola", member),
                Ct
            );
        }

        (await EraseAsync(TestSeedData.Users.MemberId)).Should().BeTrue();

        var waiting = await Factory.QueryAsync(db =>
            db.EmailOutboxMessages.AsNoTracking().Select(message => message.Kind).ToListAsync(Ct)
        );
        waiting
            .Should()
            .BeEquivalentTo(
                [EmailKind.ConsentWithdrawalNotice, EmailKind.BlockedDataConsultationNotice],
                "the notices to the legal contact are about other people"
            );
    }

    [Fact]
    public async Task EraseAsyncGuardianDecisionForAMinorIsKeptUnderTheMinor()
    {
        await SeedHouseholdAsync();
        var member = TestSeedData.Users.MemberId;
        var child = TestSeedData.Users.MemberChildId;
        await Factory.SeedAsync(db =>
        {
            db.EventTermsAcceptances.Add(
                NewDecision(
                    CampId,
                    child,
                    ConditionsId,
                    accepted: true,
                    SeededAt,
                    decidedById: member
                )
            );
            return Task.CompletedTask;
        });

        (await EraseAsync(member)).Should().BeTrue();

        (await HistoryAsync())
            .Where(item =>
                item.Entry.Entity == nameof(TermsDecisionChange)
                && GuidValue(item.Record, nameof(TermsDecisionChange.UserId)) == child
            )
            .Should()
            .ContainSingle()
            .Which.Entry.SubjectIds.Should()
            .Contain(
                [child, member],
                "the decision is about the minor and was taken by the guardian"
            );
    }

    [Fact]
    public async Task EraseAsyncMemberDeletesTheHouseholdAndKeepsEachRowInTheDataHistory()
    {
        await SeedHouseholdAsync();
        var member = TestSeedData.Users.MemberId;
        var child = TestSeedData.Users.MemberChildId;
        Guid[] household = [member, child];

        var erased = await EraseAsync(member);

        erased.Should().BeTrue();
        (await FindAsync<User>(member)).Should().BeNull();
        (await FindAsync<User>(child)).Should().BeNull();
        (await DeletedAsync<User>())
            .Select(record => GuidValue(record, nameof(User.Id)))
            .Should()
            .BeEquivalentTo(household.Select(id => (Guid?)id));
        (await DeletedAsync<Assignment>())
            .Select(record =>
                (GuidValue(record, nameof(Assignment.UserId)), GuidValue(record, "ActivityId"))
            )
            .Should()
            .BeEquivalentTo<(Guid?, Guid?)>([
                (member, ClimbingId),
                (child, ClimbingId),
                (child, WorkshopId),
            ]);
        (await DeletedAsync<EventTermsAcceptance>())
            .Select(record => GuidValue(record, "TermsDocumentId"))
            .Should()
            .BeEquivalentTo<Guid?>([ConditionsId, ImageRightsId, TripRulesId]);
        (await DeletedAsync<TermsDecisionChange>())
            .Should()
            .HaveCount(4)
            .And.OnlyContain(record => GuidValue(record, "UserId") == member);
        (await DeletedAsync<EventRating>())
            .Select(record => GuidValue(record, "EventId"))
            .Should()
            .Equal(CampId);
        (await DeletedAsync<NewsConsentChange>())
            .Where(record => GuidValue(record, "UserId") == member)
            .Should()
            .HaveCount(3);
        (await HistoryAsync())
            .Should()
            .OnlyContain(item => item.Entry.SubjectIds.Intersect(household).Any())
            .And.NotContain(item => item.Entry.SubjectIds.Contains(TestSeedData.Users.BlockedId));
        (await Factory.QueryAsync(db => db.Assignments.CountAsync(Ct)))
            .Should()
            .Be(1, "the signup of the blocked user stays");
    }

    [Fact]
    public async Task EraseAsyncNeverKeepsCredentialsCodesOrSecretsInTheDataHistory()
    {
        await SeedHouseholdAsync();

        await EraseAsync(TestSeedData.Users.MemberId);

        var record = (await DeletedAsync<User>()).Single(record =>
            GuidValue(record, nameof(User.Id)) == TestSeedData.Users.MemberId
        );
        Value(record, nameof(User.Email)).Should().Be(TestSeedData.MemberEmail);
        Value(record, nameof(User.TwoFactorMethod))
            .Should()
            .Be(nameof(TwoFactorMethod.Authenticator));
        record
            .Values.Keys.Should()
            .NotContain(key =>
                key == nameof(User.PasswordHash)
                || key == nameof(User.LastLoginAt)
                || key.StartsWith(nameof(User.Authenticator), StringComparison.Ordinal)
                || key.StartsWith(nameof(User.LoginCode), StringComparison.Ordinal)
                || key.StartsWith(nameof(User.PasswordResetCode), StringComparison.Ordinal)
                || key.StartsWith(nameof(User.VerificationCode), StringComparison.Ordinal)
                || key.StartsWith(nameof(User.LoginChallenge), StringComparison.Ordinal)
                || key.StartsWith(nameof(User.EmailChange), StringComparison.Ordinal)
                || key.StartsWith(nameof(User.TwoFactorLockout), StringComparison.Ordinal)
                || key.StartsWith(nameof(User.PasswordLockout), StringComparison.Ordinal)
            );
        var values = JsonSerializer.Serialize(record.Values);
        values.Should().NotContain(AuthenticatorKey).And.NotContain(LoginCodeHash);
        values.Should().NotContain(ResetCodeHash);
    }

    [Fact]
    public async Task EraseAsyncKeepsTheDataHistoryEncryptedAndBoundToItsEntry()
    {
        await SeedHouseholdAsync();

        await EraseAsync(TestSeedData.Users.MemberId);

        var entries = await Factory.QueryAsync(db =>
            db.DataHistory.AsNoTracking().OrderBy(entry => entry.Id).ToListAsync(Ct)
        );
        entries.Should().NotBeEmpty();
        entries
            .Select(entry => System.Text.Encoding.UTF8.GetString(entry.Payload))
            .Should()
            .NotContain(payload => payload.Contains(TestSeedData.MemberEmail));
        var protector = Factory.Services.GetRequiredService<DataHistoryProtector>();
        var moved = () => protector.Unprotect(entries[1].Id, entries[0].Payload);
        moved.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public async Task EraseAsyncWithTheDependentsTrackedStillRecordsEachUserOnce()
    {
        await SeedHouseholdAsync();

        var erased = await EraseAsync(
            TestSeedData.Users.MemberId,
            TestSeedData.Users.MemberChildId
        );

        erased.Should().BeTrue();
        (await DeletedAsync<User>()).Should().HaveCount(2);
    }

    [Fact]
    public async Task EraseAsyncUserErasedMeanwhileReturnsFalseAndRecordsNothingMore()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var stale = await db.Users.SingleAsync(
            u => u.Id == UserId.From(TestSeedData.Users.PendingId),
            Ct
        );
        await EraseAsync(TestSeedData.Users.PendingId);

        var erased = await scope
            .ServiceProvider.GetRequiredService<AccountEraser>()
            .EraseAsync(stale, Ct);

        erased.Should().BeFalse();
        (await DeletedAsync<User>()).Should().ContainSingle();
    }

    [Fact]
    public async Task EraseAsyncAccountChangedSinceItWasLoadedIsRefusedAndKeepsTheAccount()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var stale = await db.Users.SingleAsync(
            u => u.Id == UserId.From(TestSeedData.Users.PendingId),
            Ct
        );
        await Factory.SeedAsync(async other =>
            (
                await other.Users.SingleAsync(
                    u => u.Id == UserId.From(TestSeedData.Users.PendingId),
                    Ct
                )
            )
                .Verify(Factory.Clock.UtcNow)
                .IsSuccess.Should()
                .BeTrue()
        );
        await ForgetSeededHistoryAsync();

        var erase = () =>
            scope.ServiceProvider.GetRequiredService<AccountEraser>().EraseAsync(stale, Ct);

        var thrown = await erase.Should().ThrowAsync<ConcurrentUpdateException>();
        thrown.Which.EntityType.Should().Be<User>();
        (await FindAsync<User>(TestSeedData.Users.PendingId))!
            .Status.Should()
            .Be(UserStatus.Active);
        (await HistoryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EraseAsyncRefusedByTheDatabaseKeepsTheUserTheHandoverAndNoHistory()
    {
        await SeedHouseholdAsync();
        await Factory.SeedAsync(db =>
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
            return Task.CompletedTask;
        });
        await ForgetSeededHistoryAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();
        var user = await db.Users.SingleAsync(
            u => u.Id == UserId.From(TestSeedData.Users.PendingId),
            Ct
        );
        db.Users.Add(
            Persisted.As<User>(
                new
                {
                    FirstName = "Duplicado",
                    LastName = "Duplicado",
                    Email = TestSeedData.MemberEmail,
                    Gender = Gender.Other,
                    Status = UserStatus.Pending,
                    UserType = UserType.Member,
                    CreatedAt = SeededAt,
                }
            )
        );

        var erase = () =>
            scope.ServiceProvider.GetRequiredService<AccountEraser>().EraseAsync(user, Ct);

        await erase.Should().ThrowAsync<UniqueConstraintViolationException>();
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().NotBeNull();
        (await FindAsync<NewsItem>(NewsItemId))!
            .CreatedBy.Value.Should()
            .Be(TestSeedData.Users.PendingId, "the handover is rolled back with the deletion");
        (await HistoryAsync())
            .Should()
            .BeEmpty("the history commits with the change or not at all");
    }

    [Fact]
    public async Task EraseAsyncHandsEveryCreditOfTheHouseholdOverAndTheHistoryKeepsTheOriginal()
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
                KnownIds.ActivityModalityTypes.Online
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
                        ResourceType = ResourceType.Internal,
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = other,
                        UpdatedBy = member,
                    }
                )
            );
            db.Files.AddRange(
                Persisted.As<StoredFile>(
                    new
                    {
                        Id = UploadId,
                        Name = "subida",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = child,
                    }
                ),
                Persisted.As<StoredFile>(
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
            db.EventTermsAcceptances.Add(
                EventTermsAcceptance.Record(
                    EventId.From(ConferenceId),
                    UserId.From(other),
                    UserId.From(other),
                    TermsDocumentId.From(ImageRightsId),
                    1,
                    true,
                    null,
                    UserId.From(member),
                    SeededAt
                )
            );
            return Task.CompletedTask;
        });

        var erased = await EraseAsync(member);

        erased.Should().BeTrue();
        var heir = KnownIds.Users.InitialAdministrator;
        var reviewedEvent = (await FindAsync<Event>(ReviewedEventId))!;
        reviewedEvent.CreatedBy.Value.Should().Be(other);
        reviewedEvent.UpdatedBy.Should().Be(UserId.From(heir));
        var authoredActivity = (await FindAsync<Activity>(AuthoredActivityId))!;
        authoredActivity.CreatedBy.Value.Should().Be(heir);
        authoredActivity.UpdatedBy.Should().Be(UserId.From(heir));
        var newsItem = (await FindAsync<NewsItem>(NewsItemId))!;
        newsItem.CreatedBy.Value.Should().Be(heir);
        newsItem.UpdatedBy.Should().Be(UserId.From(other));
        (await FindAsync<Partner>(PartnerId))!.CreatedBy.Value.Should().Be(heir);
        var resource = (await FindAsync<Resource>(ResourceId))!;
        resource.CreatedBy.Value.Should().Be(other);
        resource.UpdatedBy.Should().Be(UserId.From(heir));
        (await FindAsync<StoredFile>(UploadId))!.UploadedBy.Value.Should().Be(heir);
        (await FindAsync<StoredFile>(OtherUploadId))!.UploadedBy.Value.Should().Be(other);
        (
            await Factory.QueryAsync(db =>
                db.TermsDecisionChanges.Where(c => c.UserId == UserId.From(other))
                    .Select(c => c.RecordedById)
                    .SingleAsync(Ct)
            )
        )
            .Value.Should()
            .Be(heir);

        var handedOver = (await HistoryAsync())
            .Where(item => item.Record.Operation is DataHistoryOperation.Modified)
            .ToList();
        handedOver
            .Select(item => item.Entry.Entity)
            .Should()
            .BeEquivalentTo([
                nameof(Event),
                nameof(Activity),
                nameof(NewsItem),
                nameof(Partner),
                nameof(Resource),
                nameof(StoredFile),
                nameof(TermsDecisionChange),
            ]);
        var originalNews = handedOver.Single(item => item.Entry.Entity == nameof(NewsItem)).Record;
        GuidValue(originalNews, "CreatedBy").Should().Be(member);
        var originalUpload = handedOver
            .Single(item => item.Entry.Entity == nameof(StoredFile))
            .Record;
        GuidValue(originalUpload, "UploadedBy").Should().Be(child);
        var originalChange = handedOver
            .Single(item => item.Entry.Entity == nameof(TermsDecisionChange))
            .Record;
        GuidValue(originalChange, "RecordedById").Should().Be(member);
        handedOver
            .Should()
            .OnlyContain(item =>
                item.Entry.SubjectIds.Contains(member) || item.Entry.SubjectIds.Contains(child)
            );
    }

    [Fact]
    public async Task EraseAsyncInitialAdministratorIsRefusedAndKeepsEverything()
    {
        await SeedHouseholdAsync();
        var administrator = KnownIds.Users.InitialAdministrator;

        var erase = () => EraseAsync(administrator);

        await erase
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*initial administrator*");
        (await FindAsync<User>(administrator)).Should().NotBeNull();
        (await FindAsync<Event>(CampId))!.CreatedBy.Value.Should().Be(administrator);
        (await HistoryAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesRemovingAUserWithoutLoadingItsRowsIsRefusedByTheDatabase()
    {
        await SeedHouseholdAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var user = await users.GetByIdAsync(UserId.From(TestSeedData.Users.BlockedId), Ct);
        users.Remove(user!);

        var save = () => uow.SaveChangesAsync(Ct);

        await save.Should()
            .ThrowAsync<DbUpdateException>("the signups it did not load would leave no history");
        (await FindAsync<User>(TestSeedData.Users.BlockedId)).Should().NotBeNull();
    }

    [Fact]
    public async Task EraseAsyncLocksEveryHouseholdRowItLoadsBeforeReadingIt()
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
    public async Task EraseAsyncAbortedByDeadlocksIsRetriedAndRecordsEachRowOnce()
    {
        await SeedHouseholdAsync();
        var deadlocks = new DeadlockInterceptor("FOR UPDATE", deadlocks: 2);

        var erased = await EraseThroughAsync(deadlocks, TestSeedData.Users.MemberId);

        erased.Should().BeTrue();
        deadlocks.Thrown.Should().Be(2);
        (await DeletedAsync<User>()).Should().HaveCount(2);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
    }

    [Fact]
    public async Task EraseAsyncAbortedByADeadlockWhileSavingIsRetriedAndRecordsEachRowOnce()
    {
        await SeedHouseholdAsync();
        var deadlocks = new DeadlockInterceptor("DELETE FROM", deadlocks: 1);

        var erased = await EraseThroughAsync(deadlocks, TestSeedData.Users.MemberId);

        erased.Should().BeTrue();
        deadlocks.Thrown.Should().Be(1);
        (await DeletedAsync<User>()).Should().HaveCount(2);
        (await DeletedAsync<Assignment>()).Should().HaveCount(3);
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
        (await HistoryAsync()).Should().BeEmpty();
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().NotBeNull();
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
                    "SELECT 1 FROM assignments WHERE user_id = @id FOR UPDATE NOWAIT",
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
