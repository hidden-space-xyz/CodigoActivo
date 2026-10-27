using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Logs;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Npgsql;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Builds the invented history of the log tables of a demo deployment over the last
/// <see cref="Span"/>: the commands its demo accounts and its administrator ran, and the warnings
/// and errors those days left. The audit entries follow the demo data, so every signup, last login
/// and published content inside the span has its entry at the instant the data shows. The
/// diagnostic entries are real events of the application, written through their declared log
/// methods and then dated, so they read exactly like the ones the API writes; those written while
/// a command ran share its trace.
/// </summary>
internal sealed class DemoLogHistory : IDisposable
{
    /// <summary>
    /// Gets how far back the history goes.
    /// </summary>
    internal static readonly TimeSpan Span = TimeSpan.FromDays(21);

    private const string ConcurrentUpdate = "ConcurrentUpdate";

    private static readonly TimeSpan SessionLength = TimeSpan.FromHours(2);
    private static readonly IPAddress AdministratorAddress = IPAddress.Parse("203.0.113.10");
    private static readonly IPAddress AttackerAddress = IPAddress.Parse("192.0.2.66");
    private static readonly Guid Administrator = InitialAdministrator.Id.Value;

    private readonly DemoLogFacts facts;
    private readonly IClock clock;
    private readonly DateTimeOffset now;
    private readonly Dictionary<Guid, DemoLogUser> users;
    private readonly Dictionary<Guid, IPAddress> addresses = new();
    private readonly Dictionary<Guid, List<DateTimeOffset>> signIns = new();
    private readonly List<LogEvent> audit = [];
    private readonly List<LogEvent> diagnostics = [];
    private readonly LastEventSink written = new();
    private readonly Logger serilog;
    private readonly SerilogLoggerFactory loggers;
    private int completedLogins;

    private DemoLogHistory(DemoLogFacts facts, IClock clock)
    {
        this.facts = facts;
        this.clock = clock;
        now = clock.UtcNow;
        users = facts.Users.ToDictionary(user => user.Id);
        for (var index = 0; index < facts.Users.Count; index++)
        {
            addresses[facts.Users[index].Id] = HomeAddress(index);
        }

        serilog = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(written)
            .CreateLogger();
        loggers = new SerilogLoggerFactory(serilog);
    }

    /// <summary>
    /// Builds the history of a demo deployment.
    /// </summary>
    /// <param name="facts">Demo data the audit entries follow.</param>
    /// <param name="clock">Clock that gives the end of the history and the local days.</param>
    /// <param name="maxFailedLogins">Wrong passwords that lock an account.</param>
    /// <param name="maxFailedCodes">Wrong codes that lock the second factor of an account.</param>
    /// <returns>The events of both log tables, oldest first; none when no demo adult is left.</returns>
    internal static DemoLogBatch Build(
        DemoLogFacts facts,
        IClock clock,
        int maxFailedLogins,
        int maxFailedCodes
    )
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(clock);

        var adults = facts.Users.Where(user => user.ParentId is null).ToList();
        if (adults.Count is 0)
        {
            return new DemoLogBatch([], []);
        }

        using var history = new DemoLogHistory(facts, clock);
        history.AddLastLogins(adults);
        history.AddSignups();
        history.AddPublishedContent();
        history.AddAdministratorMornings();
        history.AddPasswordAttack(adults[3 % adults.Count], maxFailedLogins);
        history.AddSecondFactorLockout(adults[4 % adults.Count], maxFailedCodes);
        history.AddClosedSignup(adults[5 % adults.Count]);
        history.AddProfileUpdate(adults[1 % adults.Count]);
        history.AddEventManagement();
        history.AddUserTypeChange(adults);
        history.AddEmailTrouble();
        history.AddSlowDashboards();
        history.AddDomainListFailures();
        history.AddSessionCleanups();
        return history.ToBatch();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        loggers.Dispose();
        serilog.Dispose();
    }

    private void AddLastLogins(List<DemoLogUser> adults)
    {
        var index = 0;
        foreach (var adult in adults.Where(adult => adult.LastLoginAt is { } at && InSpan(at)))
        {
            var caller = SignIn(adult.Id, adult.LastLoginAt!.Value);
            if (index++ % 3 is 0)
            {
                Record(
                    adult.LastLoginAt.Value.AddMinutes(26),
                    caller,
                    AuditAction.EndSession,
                    UserEntity(adult.Id)
                );
            }
        }
    }

    private void AddSignups()
    {
        var index = 0;
        foreach (
            var signup in facts.Signups.Where(s => InSpan(s.CreatedAt)).OrderBy(s => s.CreatedAt)
        )
        {
            var guardian = users.TryGetValue(signup.UserId, out var user) ? user.ParentId : null;
            var caller = SignedIn(guardian ?? signup.UserId, signup.CreatedAt);
            AuditEntity[] related = [UserEntity(signup.UserId)];
            var activity = ActivityEntity(signup.ActivityId);
            Record(
                signup.CreatedAt,
                caller,
                AuditAction.AssignActivity,
                activity,
                related: related
            );
            if (index++ % 9 is 4)
            {
                Record(
                    signup.CreatedAt.AddSeconds(1),
                    caller,
                    AuditAction.AssignActivity,
                    activity,
                    AuditOutcome.Rejected,
                    nameof(DomainErrorCode.ActivityAssignmentAlreadyExists),
                    related
                );
            }
        }
    }

    private void AddPublishedContent()
    {
        var published = facts
            .Events.Select(ev => new Publication(
                ev.CreatedAt,
                ev.CreatedBy,
                AuditAction.CreateEvent,
                EventEntity(ev.Id),
                null,
                ev.ThumbnailId
            ))
            .Concat(
                facts.Activities.Select(activity => new Publication(
                    activity.CreatedAt,
                    activity.CreatedBy,
                    AuditAction.CreateActivity,
                    ActivityEntity(activity.Id),
                    EventEntity(activity.EventId),
                    activity.ThumbnailId
                ))
            )
            .Concat(
                facts.News.Select(item => new Publication(
                    item.CreatedAt,
                    item.CreatedBy,
                    AuditAction.CreateNewsItem,
                    new AuditEntity(AuditEntityType.NewsItem, item.Id),
                    null,
                    item.ThumbnailId
                ))
            )
            .Where(publication => InSpan(publication.At))
            .OrderBy(publication => publication.At)
            .ThenBy(publication => publication.Action);

        var previous = DateTimeOffset.MinValue;
        foreach (var publication in published)
        {
            var at =
                publication.At > previous.AddMinutes(3) ? publication.At : previous.AddMinutes(3);
            previous = at;
            var caller = SignedIn(publication.Author, at.AddMinutes(-2));
            var thumbnail = new AuditEntity(AuditEntityType.StoredFile, publication.Thumbnail);
            Record(at.AddMinutes(-1), caller, AuditAction.CreateFile, thumbnail);
            Record(
                at,
                caller,
                publication.Action,
                publication.Entity,
                related: publication.Parent is { } parent ? [parent, thumbnail] : [thumbnail]
            );
        }
    }

    private void AddAdministratorMornings()
    {
        var reviewedUntil = now - Span;
        foreach (
            var start in Days()
                .Where(day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                .Select(day => Local(day, 9, day.DayNumber * 13 % 45))
                .Where(InSpan)
        )
        {
            var caller = SignIn(Administrator, start);
            var at = start.AddMinutes(2);
            foreach (
                var signup in facts
                    .Signups.Where(s =>
                        s.Status is not AssignmentStatus.Requested
                        && s.CreatedAt > reviewedUntil
                        && s.CreatedAt <= start
                    )
                    .OrderBy(s => s.CreatedAt)
            )
            {
                Record(
                    at,
                    caller,
                    AuditAction.ChangeAssignmentStatus,
                    ActivityEntity(signup.ActivityId),
                    related: [UserEntity(signup.UserId)]
                );
                at = at.AddSeconds(35);
            }

            reviewedUntil = start;
        }
    }

    private void AddPasswordAttack(DemoLogUser victim, int maxFailedLogins)
    {
        var start = Local(Today.AddDays(-12), 1, 14);
        var attacker = Caller.Visitor(AttackerAddress);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Record(
                start + TimeSpan.FromSeconds(attempt * 9),
                attacker,
                AuditAction.Login,
                null,
                AuditOutcome.Rejected,
                nameof(ApplicationErrorCode.InvalidCredentials)
            );
        }

        var account = UserEntity(victim.Id);
        var lockedAt = start;
        var lockingTrace = default(ActivityTraceId);
        for (var attempt = 1; attempt <= maxFailedLogins; attempt++)
        {
            lockedAt = start + TimeSpan.FromSeconds(40 + (attempt * 23));
            lockingTrace = Record(
                lockedAt,
                attacker,
                AuditAction.Login,
                account,
                AuditOutcome.Rejected,
                nameof(ApplicationErrorCode.InvalidCredentials)
            );
        }

        Diagnose<AccountLockoutLog>(
            lockedAt.AddMilliseconds(-40),
            lockingTrace,
            logger => logger.PasswordLockoutTriggered(maxFailedLogins)
        );

        var morning = Local(Today.AddDays(-11), 8, 41);
        var owner = Caller.Visitor(AddressOf(victim.Id));
        Record(
            morning,
            owner,
            AuditAction.Login,
            account,
            AuditOutcome.Rejected,
            nameof(ApplicationErrorCode.InvalidCredentials)
        );
        Record(morning.AddMinutes(1), owner, AuditAction.ForgotPassword, account);
        Record(morning.AddMinutes(5), owner, AuditAction.ResetPassword, account);
        SignIn(victim.Id, morning.AddMinutes(7));
    }

    private void AddProfileUpdate(DemoLogUser adult)
    {
        var at = Local(Today.AddDays(-1), 19, 20);
        Record(at, SignedIn(adult.Id, at), AuditAction.UpdateUser, UserEntity(adult.Id));
    }

    private void AddClosedSignup(DemoLogUser adult)
    {
        var closed = facts.Signups.OrderBy(s => s.CreatedAt).FirstOrDefault();
        if (closed is null)
        {
            return;
        }

        var at = Local(Today.AddDays(-3), 21, 47);
        Record(
            at,
            SignedIn(adult.Id, at),
            AuditAction.AssignActivity,
            ActivityEntity(closed.ActivityId),
            AuditOutcome.Rejected,
            nameof(DomainErrorCode.ActivitySignupClosed),
            [UserEntity(adult.Id)]
        );
    }

    private void AddEventManagement()
    {
        var target =
            facts
                .Signups.Where(s => InSpan(s.CreatedAt))
                .GroupBy(s => s.EventId)
                .OrderByDescending(group => group.Count())
                .Select(group => facts.Events.FirstOrDefault(ev => ev.Id == group.Key))
                .FirstOrDefault(ev => ev is not null)
            ?? (facts.Events.Count > 0 ? facts.Events[0] : null);
        if (target is not null)
        {
            AddAttendeeEmail(target);
            AddConcurrentEdit(target);
        }

        if (facts.Events.FirstOrDefault(ev => ev.Featured) is { } featured)
        {
            var at = Local(Today.AddDays(-2), 9, 52);
            Record(
                at,
                SignedIn(Administrator, at),
                AuditAction.SetEventFeatured,
                EventEntity(featured.Id)
            );
        }
    }

    private void AddAttendeeEmail(DemoLogEvent target)
    {
        var recipients = Math.Max(
            1,
            facts
                .Signups.Where(s => s.EventId == target.Id)
                .Select(s => s.UserId)
                .Distinct()
                .Count()
        );
        var at = Local(Today.AddDays(-6), 11, 2);
        var caller = SignedIn(Administrator, at);
        var failed = Record(
            at,
            caller,
            AuditAction.SendEmailToEventAttendees,
            EventEntity(target.Id),
            AuditOutcome.Rejected,
            nameof(ApplicationErrorCode.EmailSendFailed)
        );
        Diagnose<ManualEmailDispatcher>(
            at.AddMilliseconds(-60),
            failed,
            logger =>
                logger.ManualEmailQueueFailed(
                    recipients,
                    new NpgsqlException(
                        "Exception while reading from stream",
                        new TimeoutException("Timeout during reading attempt")
                    )
                )
        );

        var retried = at.AddMinutes(5);
        var sent = Record(
            retried,
            caller,
            AuditAction.SendEmailToEventAttendees,
            EventEntity(target.Id)
        );
        Diagnose<LoggingCommandDecorator<SendEmailToEventAttendeesCommand, Result<EmailDispatch>>>(
            retried.AddMilliseconds(5),
            sent,
            logger => logger.SlowUseCase(nameof(SendEmailToEventAttendeesCommand), 6_214)
        );
    }

    private void AddConcurrentEdit(DemoLogEvent target)
    {
        var at = Local(Today.AddDays(-4), 10, 31);
        var caller = SignedIn(Administrator, at);
        AuditEntity[] thumbnail = [new(AuditEntityType.StoredFile, target.ThumbnailId)];
        Record(
            at,
            caller,
            AuditAction.UpdateEvent,
            EventEntity(target.Id),
            AuditOutcome.Rejected,
            ConcurrentUpdate,
            thumbnail
        );
        Record(
            at.AddSeconds(70),
            caller,
            AuditAction.UpdateEvent,
            EventEntity(target.Id),
            related: thumbnail
        );
    }

    private void AddUserTypeChange(List<DemoLogUser> adults)
    {
        if (adults.FirstOrDefault(adult => adult.Type is UserType.Sponsor) is not { } sponsor)
        {
            return;
        }

        var at = Local(Today.AddDays(-8), 10, 15);
        Record(at, SignedIn(Administrator, at), AuditAction.ChangeUserType, UserEntity(sponsor.Id));
    }

    private void AddSecondFactorLockout(DemoLogUser adult, int maxFailedCodes)
    {
        var start = Local(Today.AddDays(-7), 20, 10);
        var visitor = Caller.Visitor(AddressOf(adult.Id));
        var account = UserEntity(adult.Id);
        Record(start, visitor, AuditAction.Login, account);
        var lockedAt = start;
        var lockingTrace = default(ActivityTraceId);
        for (var attempt = 1; attempt <= maxFailedCodes; attempt++)
        {
            lockedAt = start + TimeSpan.FromSeconds(attempt * 20);
            lockingTrace = Record(
                lockedAt,
                visitor,
                AuditAction.CompleteLogin,
                account,
                AuditOutcome.Rejected,
                nameof(ApplicationErrorCode.TwoFactorCodeInvalid)
            );
        }

        Diagnose<AccountLockoutLog>(
            lockedAt.AddMilliseconds(-40),
            lockingTrace,
            logger => logger.TwoFactorLockoutTriggered(maxFailedCodes)
        );
        Record(
            lockedAt.AddMinutes(2),
            visitor,
            AuditAction.Login,
            account,
            AuditOutcome.Rejected,
            nameof(DomainErrorCode.TwoFactorLocked)
        );
    }

    private void AddEmailTrouble()
    {
        EmailKind[] kinds =
        [
            EmailKind.TwoFactorCode,
            EmailKind.ActivityNotification,
            EmailKind.AccountVerification,
            EmailKind.Manual,
            EmailKind.AccessRecovery,
            EmailKind.SecurityAlert,
        ];
        var retried = 0;
        foreach (var day in Days().Where(day => day.DayNumber % 3 is 0))
        {
            var kind = kinds[retried++ % kinds.Length];
            Diagnose<EmailOutboxDeliverer>(
                Local(day, 7 + (day.DayNumber * 5 % 14), day.DayNumber * 17 % 60),
                default,
                logger =>
                    logger.EmailDeliveryAttemptFailed(
                        1,
                        kind,
                        new EmailDeliveryException(
                            EmailDeliveryFailure.Transient,
                            "The SMTP server rejected the message: RecipientNotAccepted (450)",
                            null
                        )
                    )
            );
        }

        var outage = Local(Today.AddDays(-9), 18, 5);
        Diagnose<EmailOutboxDeliverer>(
            outage,
            default,
            logger => logger.EmailDeliveryPaused(1, ServiceUnavailable())
        );
        Diagnose<EmailOutboxDeliverer>(
            outage.AddMinutes(17),
            default,
            logger =>
                logger.EmailDeliveryPaused(
                    10,
                    new EmailDeliveryException(
                        EmailDeliveryFailure.ServerUnavailable,
                        "Could not open a session with the SMTP server: SocketException",
                        new SocketException((int)SocketError.ConnectionRefused)
                    )
                )
        );
        Diagnose<EmailOutboxDeliverer>(
            outage.AddMinutes(18),
            default,
            logger => logger.EmailsExpired(2, EmailKind.TwoFactorCode)
        );
        Diagnose<EmailOutboxDeliverer>(
            outage.AddMinutes(38),
            default,
            logger => logger.EmailDeliveryPaused(30, ServiceUnavailable())
        );
    }

    private void AddSlowDashboards()
    {
        foreach (var (daysAgo, milliseconds) in new[] { (11, 5_481), (1, 7_032) })
        {
            var at = Local(Today.AddDays(-daysAgo), 9, 58);
            Diagnose<LoggingQueryDecorator<GetDashboardAnalyticsQuery, DashboardAnalyticsResponse>>(
                at,
                ActivityTraceId.CreateRandom(),
                logger => logger.SlowUseCase(nameof(GetDashboardAnalyticsQuery), milliseconds)
            );
        }
    }

    private void AddDomainListFailures()
    {
        Diagnose<DisposableEmailDomainRefreshWorker>(
            Local(Today.AddDays(-15), 4, 0),
            default,
            logger =>
                logger.DisposableEmailDomainDownloadFailed(
                    new HttpRequestException(
                        "Response status code does not indicate success: 503 (Service Unavailable).",
                        null,
                        HttpStatusCode.ServiceUnavailable
                    )
                )
        );
        Diagnose<DisposableEmailDomainRefreshWorker>(
            Local(Today.AddDays(-5), 4, 0),
            default,
            logger =>
                logger.DisposableEmailDomainDownloadFailed(
                    new TaskCanceledException(
                        "The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.",
                        new TimeoutException("The operation was canceled.")
                    )
                )
        );
    }

    private void AddSessionCleanups()
    {
        foreach (var day in Days())
        {
            Record(Local(day, 3, 30), Caller.Scheduled, AuditAction.RemoveExpiredSessions, null);
        }
    }

    private Caller SignedIn(Guid userId, DateTimeOffset at)
    {
        return
            signIns.TryGetValue(userId, out var instants)
            && instants.Exists(instant => instant <= at && instant > at - SessionLength)
            ? Caller.Account(userId, AddressOf(userId))
            : SignIn(userId, at.AddMinutes(-3));
    }

    private Caller SignIn(Guid userId, DateTimeOffset at)
    {
        var visitor = Caller.Visitor(AddressOf(userId));
        var account = UserEntity(userId);
        Record(at.AddSeconds(-40), visitor, AuditAction.Login, account);
        if (completedLogins++ % 7 is 3)
        {
            Record(
                at.AddSeconds(-15),
                visitor,
                AuditAction.CompleteLogin,
                account,
                AuditOutcome.Rejected,
                nameof(ApplicationErrorCode.TwoFactorCodeInvalid)
            );
        }

        Record(at, visitor, AuditAction.CompleteLogin, account);
        if (!signIns.TryGetValue(userId, out var instants))
        {
            instants = [];
            signIns[userId] = instants;
        }

        instants.Add(at);
        return Caller.Account(userId, visitor.Address!);
    }

    private ActivityTraceId Record(
        DateTimeOffset at,
        Caller caller,
        AuditAction action,
        AuditEntity? entity,
        AuditOutcome outcome = AuditOutcome.Succeeded,
        string? errorCode = null,
        IReadOnlyList<AuditEntity>? related = null
    )
    {
        var trace = caller.Type is AuditActorType.System ? default : ActivityTraceId.CreateRandom();
        if (InSpan(at))
        {
            var record = new AuditRecord(
                action,
                caller.Type,
                caller.Id,
                caller.Address,
                entity,
                related ?? [],
                outcome,
                errorCode
            );
            audit.Add(Dated(AuditLogEvents.ToEvent(record, at, activity: null), at, trace));
        }

        return trace;
    }

    private void Diagnose<TCategory>(
        DateTimeOffset at,
        ActivityTraceId trace,
        Action<ILogger> write
    )
    {
        write(loggers.CreateLogger<TCategory>());
        var logEvent = written.Take();
        if (InSpan(at))
        {
            diagnostics.Add(Dated(logEvent, at, trace));
        }
    }

    private DemoLogBatch ToBatch()
    {
        return new DemoLogBatch(
            [.. diagnostics.OrderBy(logEvent => logEvent.Timestamp)],
            [.. audit.OrderBy(logEvent => logEvent.Timestamp)]
        );
    }

    private static LogEvent Dated(LogEvent logEvent, DateTimeOffset at, ActivityTraceId trace)
    {
        return new LogEvent(
            at,
            logEvent.Level,
            logEvent.Exception,
            logEvent.MessageTemplate,
            logEvent.Properties.Select(property => new LogEventProperty(
                property.Key,
                property.Value
            )),
            trace,
            trace == default ? default : ActivitySpanId.CreateRandom()
        );
    }

    private DateOnly Today => clock.Today;

    private IEnumerable<DateOnly> Days()
    {
        return Enumerable
            .Range(0, Span.Days + 1)
            .Select(offset => Today.AddDays(offset - Span.Days));
    }

    private DateTimeOffset Local(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute));
        while (clock.TimeZone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(local, clock.TimeZone),
            TimeSpan.Zero
        );
    }

    private bool InSpan(DateTimeOffset at)
    {
        return at > now - Span && at <= now.AddMinutes(-1);
    }

    private IPAddress AddressOf(Guid userId)
    {
        return userId == Administrator ? AdministratorAddress
            : addresses.TryGetValue(userId, out var address) ? address
            : HomeAddress(addresses.Count);
    }

    private static IPAddress HomeAddress(int index)
    {
        return index % 4 is 3
            ? new IPAddress(
                new byte[]
                {
                    0x20,
                    0x01,
                    0x0d,
                    0xb8,
                    0x00,
                    0x4c,
                    0x00,
                    (byte)index,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0x2a,
                }
            )
            : new IPAddress(new byte[] { 198, 51, 100, (byte)(20 + (index * 9 % 230)) });
    }

    private static EmailDeliveryException ServiceUnavailable()
    {
        return new EmailDeliveryException(
            EmailDeliveryFailure.ServerUnavailable,
            $"The SMTP server rejected the message: {SmtpErrorCode.UnexpectedStatusCode} ({(int)SmtpStatusCode.ServiceNotAvailable})",
            null
        );
    }

    private static AuditEntity UserEntity(Guid id)
    {
        return new AuditEntity(AuditEntityType.User, id);
    }

    private static AuditEntity EventEntity(Guid id)
    {
        return new AuditEntity(AuditEntityType.Event, id);
    }

    private static AuditEntity ActivityEntity(Guid id)
    {
        return new AuditEntity(AuditEntityType.Activity, id);
    }

    private sealed record Caller(AuditActorType Type, UserId? Id, IPAddress? Address)
    {
        public static Caller Scheduled { get; } = new(AuditActorType.System, null, null);

        public static Caller Visitor(IPAddress address)
        {
            return new Caller(AuditActorType.Anonymous, null, address);
        }

        public static Caller Account(Guid id, IPAddress address)
        {
            return new Caller(AuditActorType.User, UserId.From(id), address);
        }
    }

    private sealed record Publication(
        DateTimeOffset At,
        Guid Author,
        AuditAction Action,
        AuditEntity Entity,
        AuditEntity? Parent,
        Guid Thumbnail
    );

    private sealed class LastEventSink : ILogEventSink
    {
        private LogEvent? last;

        public void Emit(LogEvent logEvent)
        {
            last = logEvent;
        }

        public LogEvent Take()
        {
            var logEvent =
                last ?? throw new InvalidOperationException("The log method wrote no event.");
            last = null;
            return logEvent;
        }
    }
}

/// <summary>
/// Holds the demo data the invented log history follows.
/// </summary>
/// <param name="Users">Demo accounts still in the database, adults and children.</param>
/// <param name="Signups">Assignments of the demo accounts.</param>
/// <param name="Events">Events the demo accounts published.</param>
/// <param name="Activities">Activities the demo accounts published.</param>
/// <param name="News">News items the demo accounts published.</param>
internal sealed record DemoLogFacts(
    IReadOnlyList<DemoLogUser> Users,
    IReadOnlyList<DemoLogSignup> Signups,
    IReadOnlyList<DemoLogEvent> Events,
    IReadOnlyList<DemoLogActivity> Activities,
    IReadOnlyList<DemoLogNewsItem> News
);

/// <summary>
/// Describes a demo account.
/// </summary>
/// <param name="Id">Account.</param>
/// <param name="ParentId">Guardian, for a child.</param>
/// <param name="Type">Kind of account.</param>
/// <param name="LastLoginAt">Last sign-in the account records.</param>
internal sealed record DemoLogUser(
    Guid Id,
    Guid? ParentId,
    UserType Type,
    DateTimeOffset? LastLoginAt
);

/// <summary>
/// Describes the assignment of a demo account to an activity.
/// </summary>
/// <param name="ActivityId">Activity.</param>
/// <param name="EventId">Event of the activity.</param>
/// <param name="UserId">Assigned account.</param>
/// <param name="Status">Current status.</param>
/// <param name="CreatedAt">When it was requested.</param>
internal sealed record DemoLogSignup(
    Guid ActivityId,
    Guid EventId,
    Guid UserId,
    AssignmentStatus Status,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Describes a published event.
/// </summary>
/// <param name="Id">Event.</param>
/// <param name="ThumbnailId">Cover image.</param>
/// <param name="Featured">Whether it is the featured event.</param>
/// <param name="CreatedBy">Author.</param>
/// <param name="CreatedAt">When it was created.</param>
internal sealed record DemoLogEvent(
    Guid Id,
    Guid ThumbnailId,
    bool Featured,
    Guid CreatedBy,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Describes a published activity.
/// </summary>
/// <param name="Id">Activity.</param>
/// <param name="EventId">Event it belongs to.</param>
/// <param name="ThumbnailId">Image.</param>
/// <param name="CreatedBy">Author.</param>
/// <param name="CreatedAt">When it was created.</param>
internal sealed record DemoLogActivity(
    Guid Id,
    Guid EventId,
    Guid ThumbnailId,
    Guid CreatedBy,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Describes a published news item.
/// </summary>
/// <param name="Id">News item.</param>
/// <param name="ThumbnailId">Image.</param>
/// <param name="CreatedBy">Author.</param>
/// <param name="CreatedAt">When it was created.</param>
internal sealed record DemoLogNewsItem(
    Guid Id,
    Guid ThumbnailId,
    Guid CreatedBy,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Holds the events of an invented log history, oldest first.
/// </summary>
/// <param name="Diagnostics">Events for <c>diagnostic_log_entries</c>.</param>
/// <param name="Audit">Events for <c>audit_log_entries</c>.</param>
internal sealed record DemoLogBatch(
    IReadOnlyList<LogEvent> Diagnostics,
    IReadOnlyList<LogEvent> Audit
);
