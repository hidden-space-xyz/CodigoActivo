using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines the policy of the email outbox: priorities, how long each kind is worth delivering, the
/// cap on administrator-written mail, the share of the daily SMTP budget each priority may use, the
/// retry and pause schedules and the batch the worker claims. It is registered with
/// <c>AddFixedOptions</c>, so no setting changes it, and the delivery worker, the tests and the
/// operator read the same policy in one place.
/// </summary>
public sealed class EmailQueueOptions
{
    /// <summary>
    /// Identifies the stored priority of the codes of someone who already proved who they are: the
    /// login code after the password and the codes that confirm deleting an account or consulting
    /// the blocked data. They leave before anything else and may spend the whole daily budget.
    /// </summary>
    public const int IdentifiedCodePriority = 0;

    /// <summary>
    /// Identifies the stored priority of the mail anyone can ask for an address: the verification
    /// of a registration or a new email, the notice that an address already has an account and the
    /// password reset. It yields to the identified codes, so a flood of it never delays a login.
    /// </summary>
    public const int AnonymousCodePriority = 1;

    /// <summary>
    /// Identifies the stored priority of the automatic notices nobody waits for in front of a screen:
    /// security alerts, activity decisions, the coming-of-age notice and the notices to the legal
    /// contact.
    /// </summary>
    public const int NoticePriority = 2;

    /// <summary>
    /// Identifies the stored priority of administrator-written mail, which yields to everything else
    /// because one batch can hold hundreds of recipients.
    /// </summary>
    public const int BulkPriority = 3;

    /// <summary>
    /// Identifies the default of <see cref="BatchSize"/>, the number of due messages claimed at once.
    /// </summary>
    public const int DefaultBatchSize = 10;

    /// <summary>
    /// Identifies how many different messages in a row may fail for a reason of their own before the
    /// worker blames the server and pauses: one rejection is the message's, three in a row are the
    /// server's. Copies of one content, such as an administrator's mailing, count once.
    /// </summary>
    public const int FailuresBeforePause = 3;

    /// <summary>
    /// Stores how long a notice or administrator-written mail is worth delivering. A code says its
    /// own, shorter lifetime through <see cref="EmailMessage.ValidFor"/>.
    /// </summary>
    public static readonly TimeSpan MessageLifetime = TimeSpan.FromDays(3);

    /// <summary>
    /// Stores the age after which a message that never expires is reported as overdue.
    /// </summary>
    public static readonly TimeSpan OverdueAge = TimeSpan.FromHours(24);

    /// <summary>
    /// Stores the rolling window the daily limit of the SMTP provider is counted over.
    /// </summary>
    public static readonly TimeSpan DailyWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Stores the extra time added to a lease on top of the worst case of the claimed batch, which
    /// covers the work the run does around the sends themselves: claiming the rows, reading the
    /// stored content, recording each result and the clock difference between two instances.
    /// </summary>
    public static readonly TimeSpan LeaseMargin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stores the delay applied after the first, second and third failure of a message and, from
    /// the fourth on, after every further one, until it expires.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
    ];

    /// <summary>
    /// Stores how long the whole delivery pauses after the first, second, third and fourth server
    /// failure in a row and, from the fifth on, after every further one. A pause spends no attempt of
    /// any message.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> PauseDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30),
    ];

    /// <summary>
    /// Gets or sets how many administrator-written messages may wait at once; a batch that would pass
    /// it is refused with an error the administrator sees.
    /// </summary>
    public int Capacity { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the number of due messages claimed at once, sent one after another over one
    /// connection. A run that claims this many is followed at once by the next one instead of
    /// waiting for <see cref="PollInterval"/>.
    /// </summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>
    /// Gets or sets how long stopping the host waits for the send in flight. A message not delivered
    /// by then stays in the outbox and is claimed again once its lease expires.
    /// </summary>
    public TimeSpan ShutdownDrain { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Gets or sets the longest time one send may take, connection to the SMTP server and
    /// authentication included; a send still running then is cancelled and counts as a failed
    /// attempt.
    /// </summary>
    public TimeSpan SendTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets how long the worker waits for new work before looking for due messages again.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets how long a claimed message stays reserved for the worker that took it: the messages of a
    /// batch are sent one after another, so the last one is done within <see cref="BatchSize"/>
    /// sends, each of which may spend a full <see cref="SendTimeout"/>. The lease covers that worst
    /// case plus <see cref="LeaseMargin"/>, so no row becomes claimable again while the worker that
    /// claimed it may still be delivering it.
    /// </summary>
    public TimeSpan Lease =>
        (Math.Max(BatchSize, 1) * (SendTimeout > TimeSpan.Zero ? SendTimeout : TimeSpan.Zero))
        + LeaseMargin;

    /// <summary>
    /// Gets the stored delivery priority of an email kind. The claim orders by this column first.
    /// </summary>
    /// <param name="kind">Email category whose priority is wanted.</param>
    /// <returns>One of the four priorities; lower values are claimed first.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no priority assigned.</exception>
    public static int PriorityOf(EmailKind kind)
    {
        return kind switch
        {
            EmailKind.TwoFactorCode => IdentifiedCodePriority,
            EmailKind.AccountVerification or EmailKind.AccessRecovery => AnonymousCodePriority,
            EmailKind.SecurityAlert
            or EmailKind.ActivityNotification
            or EmailKind.ComingOfAgeNotice
            or EmailKind.ConsentWithdrawalNotice
            or EmailKind.BlockedDataConsultationNotice => NoticePriority,
            EmailKind.Manual => BulkPriority,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>
    /// Gets how long a message of a kind is worth delivering when it does not say so itself.
    /// </summary>
    /// <param name="kind">Email category of the message.</param>
    /// <returns>
    /// <see cref="MessageLifetime"/>, or <see langword="null"/> for the notices that must arrive
    /// however late: the coming-of-age notice and the notices to the legal contact.
    /// </returns>
    public static TimeSpan? LifetimeOf(EmailKind kind)
    {
        return
            kind
                is EmailKind.ComingOfAgeNotice
                    or EmailKind.ConsentWithdrawalNotice
                    or EmailKind.BlockedDataConsultationNotice
            ? null
            : MessageLifetime;
    }

    /// <summary>
    /// Determines whether the email guard limits a kind before it is stored: only the codes, which
    /// someone asks for and which are useless once late, so a refusal is answered at once.
    /// </summary>
    /// <param name="kind">Email category being stored.</param>
    /// <returns><see langword="true"/> for the kinds of the two code priorities.</returns>
    public static bool IsGuarded(EmailKind kind)
    {
        return PriorityOf(kind) <= AnonymousCodePriority;
    }

    /// <summary>
    /// Determines whether a kind counts against <see cref="Capacity"/>: only administrator-written
    /// mail, whose refusal the administrator sees and can repeat later.
    /// </summary>
    /// <param name="kind">Email category being stored.</param>
    /// <returns><see langword="true"/> for <see cref="EmailKind.Manual"/>.</returns>
    public static bool IsCapped(EmailKind kind)
    {
        return PriorityOf(kind) == BulkPriority;
    }

    /// <summary>
    /// Determines whether messages of a priority are paced per recipient when delivered, so actions
    /// repeated in a loop never flood a mailbox or spend the budget of the SMTP provider.
    /// </summary>
    /// <param name="priority">Stored priority of the message.</param>
    /// <returns><see langword="true"/> for notices and administrator-written mail.</returns>
    public static bool IsPacedPerRecipient(int priority)
    {
        return priority >= NoticePriority;
    }

    /// <summary>
    /// Gets the percentage of the daily limit of the SMTP provider that messages of a priority may
    /// use: the identified codes all of it, the anonymous codes 90 % and the rest 80 %, so a flood
    /// of mail anyone can ask for never leaves a login without its code.
    /// </summary>
    /// <param name="priority">Stored priority of the message.</param>
    /// <returns>The percentage.</returns>
    public static int DailySharePercent(int priority)
    {
        return priority switch
        {
            IdentifiedCodePriority => 100,
            AnonymousCodePriority => 90,
            _ => 80,
        };
    }

    /// <summary>
    /// Gets the lowest priority, that is the highest stored value, that may still be claimed with
    /// the messages already sent within <see cref="DailyWindow"/>.
    /// </summary>
    /// <param name="sentInWindow">Messages handed to the SMTP server within the window.</param>
    /// <param name="dailyLimit">Messages the SMTP provider accepts per day.</param>
    /// <returns>
    /// The highest claimable priority value, or <see langword="null"/> when the whole daily limit is
    /// spent.
    /// </returns>
    public static int? ClaimablePriority(long sentInWindow, int dailyLimit)
    {
        for (var priority = BulkPriority; priority >= IdentifiedCodePriority; priority--)
        {
            if (sentInWindow * 100 < (long)dailyLimit * DailySharePercent(priority))
            {
                return priority;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the delay that separates the supplied number of failed attempts from the next one.
    /// </summary>
    /// <param name="attempts">Number of attempts that have already failed.</param>
    /// <returns>
    /// The matching entry of <see cref="RetryDelays"/>; counts below one take the first entry and
    /// counts past the end of the list take the last.
    /// </returns>
    public static TimeSpan RetryDelayAfter(int attempts)
    {
        return Pick(RetryDelays, attempts);
    }

    /// <summary>
    /// Gets how long the delivery pauses after the supplied number of server failures in a row.
    /// </summary>
    /// <param name="failures">Number of server failures in a row, the current one included.</param>
    /// <returns>
    /// The matching entry of <see cref="PauseDelays"/>; counts below one take the first entry and
    /// counts past the end of the list take the last.
    /// </returns>
    public static TimeSpan PauseDelayAfter(int failures)
    {
        return Pick(PauseDelays, failures);
    }

    private static TimeSpan Pick(IReadOnlyList<TimeSpan> delays, int count)
    {
        return delays[Math.Clamp(count - 1, 0, delays.Count - 1)];
    }
}
