using System.Security.Cryptography;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.DataHistory;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Replays on the demo data a year of changes and erasures of personal data, so the blocked data
/// have a history to consult from the first start. Each change is saved through a data history
/// recorder of its own, at the moment it would have happened and as the command and the account
/// that would have made it: demo accounts that replaced their phone and email, withdrew their news
/// consent, left an activity or deleted a rating, and three former accounts erased with everything
/// that belonged to them, a household with a minor by its holder, a sponsor by the administrator
/// and an unverified registration by the scheduled purge. It runs once, in one transaction, when
/// the history holds nothing about the former accounts.
/// </summary>
/// <param name="options">Options of the application database.</param>
/// <param name="protector">Protector that encrypts the history records.</param>
/// <param name="codeHasher">Hasher of the discarded code that confirms the replaced email.</param>
/// <param name="verification">Lifetime of that code.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class DemoDataHistorySeeder(
    DbContextOptions<CodigoActivoDbContext> options,
    DataHistoryProtector protector,
    IOneTimeCodeHasher codeHasher,
    AccountVerificationOptions verification,
    IClock clock
)
{
    private const string DemoEmailDomain = $"demo.{Organization.WebDomain}";

    private static readonly UserId FormerGuardianId = DemoDataSeeder.DemoUser(100);
    private static readonly UserId FormerChildId = DemoDataSeeder.DemoUser(101);
    private static readonly UserId FormerSponsorId = DemoDataSeeder.DemoUser(102);
    private static readonly UserId UnverifiedId = DemoDataSeeder.DemoUser(103);

    private static readonly PersonDetails FormerGuardian = new(
        "Sergio",
        "Lozano Prieto",
        Gender.Male,
        $"sergio.lozano.prieto@{DemoEmailDomain}",
        "+34661000100",
        NationalId: SpanishNationalId.FromDniNumber(71_234_100).Value,
        NewsConsent: true
    );

    private static readonly PersonDetails FormerSponsor = new(
        "Marcos",
        "Ibáñez Robles",
        Gender.Male,
        $"marcos.ibanez.robles@{DemoEmailDomain}",
        "+34661000102",
        NationalId: SpanishNationalId.FromDniNumber(71_234_102).Value
    );

    private static readonly PersonDetails Unverified = new(
        "Patricia",
        "Gallego Herrera",
        Gender.Female,
        $"patricia.gallego.herrera@{DemoEmailDomain}",
        "+34661000103",
        NationalId: SpanishNationalId.FromDniNumber(71_234_103).Value,
        NewsConsent: true
    );

    /// <summary>
    /// Replays the history when it holds nothing about the former demo accounts.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await using var replay = new Replay(options, protector, clock.TimeZone);
        var context = replay.Context;
        if (
            await context.DataHistory.AnyAsync(
                entry => entry.SubjectIds.Contains(FormerGuardianId.Value),
                ct
            )
        )
        {
            return;
        }

        var now = clock.UtcNow;
        var demoIds = DemoDataSeeder.UserIds.ToList();
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var events = (await context.Events.ToListAsync(ct))
            .OrderBy(ev => ev.Calendar.Start)
            .ToList();
        var activities = (await context.Activities.Include(a => a.Assignments).ToListAsync(ct))
            .OrderBy(activity => activity.Schedule.StartsAt)
            .ToList();
        var adults = await context
            .Users.Where(user => demoIds.Contains(user.Id) && user.ParentId == null)
            .OrderBy(user => user.Id)
            .ToListAsync(ct);
        var ratings = await context.EventRatings.OrderBy(rating => rating.Id).ToListAsync(ct);

        await EraseHouseholdAsync(replay, events, activities, now, ct);
        await DeleteSponsorAsync(replay, events, activities, now, ct);
        await PurgeUnverifiedAsync(replay, now, ct);
        await ReplaceContactAsync(replay, adults[3], now, ct);
        await WithdrawNewsConsentAsync(replay, adults.Skip(6).First(u => u.NewsConsent), now, ct);
        var adultIds = adults.Select(user => user.Id).ToHashSet();
        await LeaveActivityAsync(replay, activities, adultIds, now, ct);
        await DeleteRatingAsync(replay, ratings, events, adultIds, now, ct);

        await transaction.CommitAsync(ct);
    }

    private static async Task EraseHouseholdAsync(
        Replay replay,
        List<Event> events,
        List<Activity> activities,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var registeredAt = now.AddDays(-400);
        var erasedAt = now.AddDays(-95);
        var guardian = await RegisterAsync(
            replay,
            FormerGuardian,
            FormerGuardianId,
            registeredAt,
            ct
        );

        var childAddedAt = registeredAt.AddDays(3);
        var childToday = replay.LocalDate(childAddedAt);
        var child = User.CreateDependent(
            guardian,
            new PersonDetails(
                "Nora",
                "Lozano Prieto",
                Gender.Female,
                BirthDate: new DateOnly(childToday.Year - 8, 3, 14)
            ),
            declaresGuardianship: true,
            childToday,
            childAddedAt,
            FormerChildId
        ).Value;
        replay.Context.Users.Add(child);
        await replay.SaveAsync(childAddedAt, guardian.Id, typeof(AddChildCommand), ct);

        var attended = events
            .Where(ev =>
                ev.SignupWindow.StartsAt > childAddedAt
                && ev.Calendar.End < replay.LocalDate(erasedAt.AddDays(-10))
            )
            .Take(2)
            .ToList();
        foreach (var ev in attended)
        {
            await SignUpAsync(replay, activities, ev, [guardian, child], ct);
        }

        if (attended.FirstOrDefault() is { } rated)
        {
            replay.Context.EventRatings.Add(
                EventRating
                    .Submit(
                        rated.Id,
                        guardian.Id,
                        4,
                        "Que mi hija pudiera programar su primer juego sin ayuda.",
                        "Los turnos de la tarde se alargaron más de lo previsto.",
                        "Dejar los materiales publicados después de cada sesión."
                    )
                    .Value
            );
            await replay.SaveAsync(
                replay.LocalTime(rated.Calendar.End.AddDays(2), 21),
                guardian.Id,
                typeof(SaveEventRatingCommand),
                ct
            );
        }

        var movedAt = now.AddDays(-230);
        guardian.ApplyProfileChange(
            guardian
                .PlanProfileChange(
                    FormerGuardian with
                    {
                        Phone = "+34671000100",
                    },
                    null,
                    replay.LocalDate(movedAt)
                )
                .Value,
            movedAt
        );
        await replay.SaveAsync(movedAt, guardian.Id, typeof(UpdateUserCommand), ct);

        await EraseAsync(
            replay,
            guardian,
            erasedAt,
            guardian.Id,
            typeof(DeleteOwnAccountCommand),
            ct
        );
    }

    private static async Task DeleteSponsorAsync(
        Replay replay,
        List<Event> events,
        List<Activity> activities,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var registeredAt = now.AddDays(-280);
        var deletedAt = now.AddDays(-30);
        var sponsor = await RegisterAsync(replay, FormerSponsor, FormerSponsorId, registeredAt, ct);

        var promotedAt = registeredAt.AddDays(5);
        sponsor.ChangeType(UserType.Sponsor, promotedAt);
        await replay.SaveAsync(
            promotedAt,
            InitialAdministrator.Id,
            typeof(ChangeUserTypeCommand),
            ct
        );

        if (
            events.LastOrDefault(ev =>
                ev.SignupWindow.StartsAt > promotedAt
                && ev.Calendar.End < replay.LocalDate(deletedAt.AddDays(-10))
            ) is
            { } attended
        )
        {
            await SignUpAsync(replay, activities, attended, [sponsor], ct);
        }

        await EraseAsync(
            replay,
            sponsor,
            deletedAt,
            InitialAdministrator.Id,
            typeof(DeleteUserCommand),
            ct
        );
    }

    private static async Task PurgeUnverifiedAsync(
        Replay replay,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var registeredAt = now.AddDays(-12);
        var account = User.CreateIndependent(
            Unverified,
            declaresAdult: true,
            registeredAt,
            UnverifiedId
        ).Value;
        replay.Context.Users.Add(account);
        await replay.SaveAsync(registeredAt, null, typeof(RegisterCommand), ct);

        await EraseAsync(
            replay,
            account,
            registeredAt.AddDays(User.UnverifiedRetentionDays).AddMinutes(40),
            null,
            typeof(PurgeUnverifiedAccountsCommand),
            ct
        );
    }

    private async Task ReplaceContactAsync(
        Replay replay,
        User adult,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var changedAt = Later(adult.CreatedAt.AddDays(30), now.AddDays(-160));
        var email = adult.Email!.Value;
        var change = adult
            .PlanProfileChange(
                new PersonDetails(
                    adult.FirstName,
                    adult.LastName,
                    adult.Gender,
                    email.Insert(email.IndexOf('@', StringComparison.Ordinal), ".personal"),
                    $"+3467{adult.Phone!.Value[5..]}",
                    adult.SecondaryPhone?.Value,
                    adult.NationalId!.Value,
                    adult.NewsConsent
                ),
                null,
                replay.LocalDate(changedAt)
            )
            .Value;
        adult.ApplyProfileChangeConfirmingEmail(
            change,
            codeHasher.Hash(RandomNumberGenerator.GetHexString(16)),
            changedAt,
            verification.OtpLifetime
        );
        await replay.SaveAsync(changedAt, adult.Id, typeof(UpdateUserCommand), ct);

        var confirmedAt = changedAt.AddMinutes(9);
        adult.ConfirmEmailChange(confirmedAt);
        await replay.SaveAsync(confirmedAt, adult.Id, typeof(ConfirmEmailChangeCommand), ct);
    }

    private static async Task WithdrawNewsConsentAsync(
        Replay replay,
        User adult,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var withdrawnAt = Later(adult.CreatedAt.AddDays(20), now.AddDays(-70));
        adult.WithdrawNewsConsent(NewsConsentSource.UnsubscribeLink, withdrawnAt);
        await replay.SaveAsync(withdrawnAt, null, typeof(UnsubscribeCommand), ct);
    }

    private static async Task LeaveActivityAsync(
        Replay replay,
        List<Activity> activities,
        HashSet<UserId> adultIds,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var left = activities
            .Where(activity => activity.Schedule.StartsAt > now.AddDays(2))
            .SelectMany(activity =>
                activity
                    .Assignments.Where(signup =>
                        adultIds.Contains(signup.UserId) && signup.CreatedAt < now.AddDays(-4)
                    )
                    .Select(signup => (Activity: activity, Signup: signup))
            )
            .FirstOrDefault();
        if (left.Activity is null)
        {
            return;
        }

        var leftAt = left.Signup.CreatedAt + ((now - left.Signup.CreatedAt) / 2);
        left.Activity.Unassign(left.Signup.UserId);
        await replay.SaveAsync(leftAt, left.Signup.UserId, typeof(UnassignActivityCommand), ct);
    }

    private static async Task DeleteRatingAsync(
        Replay replay,
        List<EventRating> ratings,
        List<Event> events,
        HashSet<UserId> adultIds,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var deletedAt = now.AddDays(-15);
        var deleted = ratings
            .Where(rating => adultIds.Contains(rating.UserId))
            .Select(rating => (Rating: rating, Event: events.Single(ev => ev.Id == rating.EventId)))
            .Where(rated => replay.LocalTime(rated.Event.Calendar.End.AddDays(5), 0) < deletedAt)
            .OrderByDescending(rated => rated.Event.Calendar.End)
            .FirstOrDefault();
        if (deleted.Rating is null)
        {
            return;
        }

        replay.Context.EventRatings.Remove(deleted.Rating);
        await replay.SaveAsync(
            deletedAt,
            deleted.Rating.UserId,
            typeof(DeleteEventRatingCommand),
            ct
        );
    }

    private static async Task<User> RegisterAsync(
        Replay replay,
        PersonDetails details,
        UserId id,
        DateTimeOffset registeredAt,
        CancellationToken ct
    )
    {
        var account = User.CreateIndependent(details, declaresAdult: true, registeredAt, id).Value;
        replay.Context.Users.Add(account);
        await replay.SaveAsync(registeredAt, null, typeof(RegisterCommand), ct);

        var verifiedAt = registeredAt.AddMinutes(6);
        account.Verify(verifiedAt);
        await replay.SaveAsync(verifiedAt, null, typeof(VerifyUserCommand), ct);
        return account;
    }

    private static async Task SignUpAsync(
        Replay replay,
        List<Activity> activities,
        Event ev,
        User[] people,
        CancellationToken ct
    )
    {
        var activity = activities.First(candidate => candidate.EventId == ev.Id);
        var signedUpAt = ev.SignupWindow.StartsAt.AddHours(26);
        foreach (var person in people)
        {
            activity.RequestAssignment(person.Id, ActivityRole.Participant, signedUpAt);
        }

        await replay.SaveAsync(
            signedUpAt,
            people[0].Id,
            people.Length > 1 ? typeof(AssignHouseholdCommand) : typeof(AssignActivityCommand),
            ct
        );

        foreach (var person in people)
        {
            activity.ChangeAssignmentStatus(person.Id, AssignmentStatus.Confirmed);
        }

        await replay.SaveAsync(
            signedUpAt.AddDays(2),
            InitialAdministrator.Id,
            typeof(ChangeAssignmentStatusCommand),
            ct
        );
    }

    private static async Task EraseAsync(
        Replay replay,
        User account,
        DateTimeOffset erasedAt,
        UserId? actor,
        Type command,
        CancellationToken ct
    )
    {
        var store = new AccountErasureStore(replay.Context);
        await store.LoadHouseholdAsync(account.Id, ct);
        await store.HandOverAuthoredContentAsync(account.Id, InitialAdministrator.Id, ct);
        account.Erase();
        replay.Context.Users.Remove(account);
        await replay.SaveAsync(erasedAt, actor, command, ct);
    }

    private static DateTimeOffset Later(DateTimeOffset first, DateTimeOffset second)
    {
        return first > second ? first : second;
    }

    /// <summary>
    /// Database context whose saves record their data history at a moment of the past, as the
    /// command and the account that would have made them.
    /// </summary>
    private sealed class Replay : IClock, ICurrentUser, IAsyncDisposable
    {
        private readonly CommandScope commands = new();

        public Replay(
            DbContextOptions<CodigoActivoDbContext> options,
            DataHistoryProtector protector,
            TimeZoneInfo timeZone
        )
        {
            TimeZone = timeZone;
            Context = new CodigoActivoDbContext(
                options,
                new DataHistoryRecorder(protector, this, commands, this)
            );
        }

        public CodigoActivoDbContext Context { get; }

        public DateTimeOffset UtcNow { get; private set; }

        public DateOnly Today => LocalDate(UtcNow);

        public TimeZoneInfo TimeZone { get; }

        public UserId? Id { get; private set; }

        public bool IsAdmin => Id == InitialAdministrator.Id;

        public async Task SaveAsync(
            DateTimeOffset at,
            UserId? actor,
            Type command,
            CancellationToken ct
        )
        {
            UtcNow = at;
            Id = actor;
            using (commands.Enter(CommandAudit.NameOf(command)))
            {
                await Context.SaveChangesAsync(ct);
            }
        }

        public DateOnly LocalDate(DateTimeOffset at)
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, TimeZone).DateTime);
        }

        public DateTimeOffset LocalTime(DateOnly day, int hour)
        {
            return new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, 0)), TimeZone),
                TimeSpan.Zero
            );
        }

        public ValueTask DisposeAsync()
        {
            return Context.DisposeAsync();
        }
    }
}
