using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines the policy of the email outbox: capacity, concurrency, leases, the retry schedule and the
/// delivery priority of each email kind. It is registered with <c>AddFixedOptions</c>, so no setting
/// changes it, and the delivery worker, the tests and the operator read the same policy in one place.
/// </summary>
public sealed class EmailQueueOptions
{
    /// <summary>
    /// Identifies the stored priority of the interactive email a person is waiting for: the login
    /// code, the password reset and the account verification. These leave before anything else and
    /// a full outbox never holds them back.
    /// </summary>
    public const int CriticalPriority = 0;

    /// <summary>
    /// Identifies the stored priority of the automatic mail to one recipient that nobody is waiting in
    /// front of a screen for: security alerts, activity decisions, the coming-of-age notice to a
    /// guardian and the consent withdrawal and blocked data consultation notices to the association.
    /// </summary>
    public const int StandardPriority = 1;

    /// <summary>
    /// Identifies the stored priority of administrator-written mail, which yields to everything else
    /// because one batch can hold hundreds of recipients.
    /// </summary>
    public const int BulkPriority = 2;

    /// <summary>
    /// Identifies the default of <see cref="Workers"/>, the number of messages delivered concurrently.
    /// </summary>
    public const int DefaultWorkers = 4;

    /// <summary>
    /// Identifies the default of <see cref="BatchSize"/>, the number of due messages claimed at once.
    /// </summary>
    public const int DefaultBatchSize = 10;

    /// <summary>
    /// Identifies how many times a message is attempted in total before it is discarded.
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Stores the extra time added to a lease on top of the worst case of the claimed batch, which
    /// covers the work the run does around the sends themselves: claiming the rows, reading the
    /// stored content, recording each result and the clock difference between two instances.
    /// </summary>
    public static readonly TimeSpan LeaseMargin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stores the delay applied after the first, second, third and fourth failure of a message.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
    ];

    /// <summary>
    /// Gets or sets the capacity value, the approximate number of messages the outbox keeps pending.
    /// It is a soft limit: the count that guards an enqueue and the insert that follows it run at the
    /// READ COMMITTED isolation of the database, so batches stored at the same time do not see each
    /// other and the pending rows may exceed this value by the recipients of the batches that were in
    /// flight. The kinds <see cref="IsCapacityExempt"/> accepts are stored past it and still occupy
    /// the pending rows it counts.
    /// </summary>
    public int Capacity { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the workers value, the number of messages delivered concurrently.
    /// </summary>
    public int Workers { get; set; } = DefaultWorkers;

    /// <summary>
    /// Gets or sets the number of due messages claimed at once. A run that claims this many is
    /// followed at once by the next one instead of waiting for <see cref="PollInterval"/>.
    /// </summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>
    /// Gets or sets how long stopping the host waits for the sends already in flight. A message not
    /// delivered by then stays in the outbox with its attempt counted and is claimed again once its
    /// lease expires.
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
    /// Gets how long a claimed message stays reserved for the worker that took it. The whole batch is
    /// leased when it is claimed, and <see cref="Workers"/> messages of it are delivered at a time, so
    /// the last message of a batch of <see cref="BatchSize"/> may only start after the rounds before
    /// it and is done within <see cref="DeliveryWaves"/> rounds, each of which may spend a full
    /// <see cref="SendTimeout"/>. The lease covers that worst case plus <see cref="LeaseMargin"/>: no
    /// row becomes claimable again by another instance while the worker that claimed it may still be
    /// delivering it. It grows with the batch and shrinks with the workers.
    /// </summary>
    public TimeSpan Lease => (DeliveryWaves * PositiveSendTimeout) + LeaseMargin;

    /// <summary>
    /// Gets how many rounds of concurrent sends one claimed batch is delivered in:
    /// <see cref="BatchSize"/> divided by <see cref="Workers"/>, rounded up, with values below one
    /// counted as one.
    /// </summary>
    public int DeliveryWaves
    {
        get
        {
            var workers = Math.Max(Workers, 1);
            return ((Math.Max(BatchSize, 1) - 1) / workers) + 1;
        }
    }

    private TimeSpan PositiveSendTimeout =>
        SendTimeout > TimeSpan.Zero ? SendTimeout : TimeSpan.Zero;

    /// <summary>
    /// Gets the stored delivery priority of an email kind. The claim orders by this column first, so
    /// a login code never queues behind a bulk mailing that is already due.
    /// </summary>
    /// <param name="kind">Email category whose priority is wanted.</param>
    /// <returns>
    /// <see cref="CriticalPriority"/>, <see cref="StandardPriority"/> or, only for
    /// administrator-written mail, <see cref="BulkPriority"/>; lower values are claimed first.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no priority assigned.</exception>
    public static int PriorityOf(EmailKind kind)
    {
        return kind switch
        {
            EmailKind.TwoFactorCode or EmailKind.AccessRecovery or EmailKind.AccountVerification =>
                CriticalPriority,
            EmailKind.SecurityAlert
            or EmailKind.ActivityNotification
            or EmailKind.ComingOfAgeNotice
            or EmailKind.ConsentWithdrawalNotice
            or EmailKind.BlockedDataConsultationNotice => StandardPriority,
            EmailKind.Manual => BulkPriority,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>
    /// Determines whether an email kind is accepted even when the outbox already holds
    /// <see cref="Capacity"/> messages. A full outbox, which an unreachable SMTP server and a few
    /// bulk mailings are enough to produce, must never stop a person from completing a login,
    /// resetting a password or verifying a new account, nor lose the notice that tells the
    /// association a consent was withdrawn or the blocked data were consulted, which nothing sends
    /// again. Those kinds stay bounded by the per-recipient quota and the global budget of the email
    /// guard instead.
    /// </summary>
    /// <param name="kind">Email category being stored.</param>
    /// <returns>
    /// <see langword="true"/> for the kinds of <see cref="CriticalPriority"/> and the consent
    /// withdrawal and blocked data consultation notices; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsCapacityExempt(EmailKind kind)
    {
        return PriorityOf(kind) == CriticalPriority
            || kind is EmailKind.ConsentWithdrawalNotice or EmailKind.BlockedDataConsultationNotice;
    }

    /// <summary>
    /// Determines whether a message that has failed the supplied number of times is out of attempts
    /// and has to be discarded.
    /// </summary>
    /// <param name="attempts">Number of attempts that have already failed.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="attempts"/> has reached <see cref="MaxAttempts"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsExhausted(int attempts)
    {
        return attempts >= MaxAttempts;
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
        var index = Math.Clamp(attempts - 1, 0, RetryDelays.Count - 1);
        return RetryDelays[index];
    }
}
