using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines configuration values for email queue. The retry schedule and the delivery priority of
/// each email kind live here so the delivery worker, the tests and the operator read the same policy
/// in one place.
/// </summary>
public sealed class EmailQueueOptions
{
    /// <summary>
    /// Identifies the stored priority of the interactive email a person is waiting for: the login
    /// code, the password reset and the account verification. These leave before anything else and
    /// are the kinds <see cref="IsCapacityExempt"/> keeps accepting with a full outbox.
    /// </summary>
    public const int CriticalPriority = 0;

    /// <summary>
    /// Identifies the stored priority of the automatic mail the application sends on its own, which
    /// nobody is waiting in front of a screen for.
    /// </summary>
    public const int StandardPriority = 1;

    /// <summary>
    /// Identifies the stored priority of bulk administrator-written mail, which yields to everything
    /// else because one batch can hold hundreds of recipients.
    /// </summary>
    public const int BulkPriority = 2;

    /// <summary>
    /// Identifies the default capacity configuration or policy value.
    /// </summary>
    public const int DefaultCapacity = 1000;

    /// <summary>
    /// Identifies the default workers configuration or policy value.
    /// </summary>
    public const int DefaultWorkers = 4;

    /// <summary>
    /// Identifies the max workers configuration or policy value.
    /// </summary>
    public const int MaxWorkers = 16;

    /// <summary>
    /// Identifies the default batch size configuration or policy value.
    /// </summary>
    public const int DefaultBatchSize = 10;

    /// <summary>
    /// Identifies the max batch size configuration or policy value.
    /// </summary>
    public const int MaxBatchSize = 100;

    /// <summary>
    /// Identifies how many times a message is attempted in total before it is discarded.
    /// </summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Stores the shared default shutdown drain value.
    /// </summary>
    public static readonly TimeSpan DefaultShutdownDrain = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Stores the shared default send timeout value.
    /// </summary>
    public static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Stores the shared max shutdown drain value.
    /// </summary>
    public static readonly TimeSpan MaxShutdownDrain = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Stores the shared max send timeout value.
    /// </summary>
    public static readonly TimeSpan MaxSendTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Stores the shared default poll interval value.
    /// </summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Stores the shared max poll interval value.
    /// </summary>
    public static readonly TimeSpan MaxPollInterval = TimeSpan.FromMinutes(5);

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
    /// flight. It only holds back the mail nobody is waiting for: the kinds
    /// <see cref="IsCapacityExempt"/> accepts are stored past it and still occupy the pending rows it
    /// counts.
    /// </summary>
    public int Capacity { get; set; } = DefaultCapacity;

    /// <summary>
    /// Gets or sets the workers value, the number of messages delivered concurrently.
    /// </summary>
    public int Workers { get; set; } = DefaultWorkers;

    /// <summary>
    /// Gets or sets the number of due messages claimed at once.
    /// </summary>
    public int BatchSize { get; set; } = DefaultBatchSize;

    /// <summary>
    /// Gets or sets the shutdown drain value.
    /// </summary>
    public TimeSpan ShutdownDrain { get; set; } = DefaultShutdownDrain;

    /// <summary>
    /// Gets or sets the send timeout value.
    /// </summary>
    public TimeSpan SendTimeout { get; set; } = DefaultSendTimeout;

    /// <summary>
    /// Gets or sets how long the worker waits for new work before looking for due messages again.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = DefaultPollInterval;

    /// <summary>
    /// Gets how long a claimed message stays reserved for the worker that took it. The whole batch is
    /// leased when it is claimed, and <see cref="Workers"/> messages of it are delivered at a time, so
    /// the last message of a batch of <see cref="BatchSize"/> waits for the
    /// <see cref="DeliveryWaves"/> rounds before it, each of which may spend a full
    /// <see cref="SendTimeout"/>. The lease covers that worst case plus <see cref="LeaseMargin"/>: no
    /// row becomes claimable again by another instance while the worker that claimed it may still be
    /// delivering it. It grows with the configured batch and shrinks with the workers, and the
    /// configuration bounds of <see cref="MaxBatchSize"/> messages, one worker and
    /// <see cref="MaxSendTimeout"/> keep it under 17 hours.
    /// </summary>
    public TimeSpan Lease => (DeliveryWaves * PositiveSendTimeout) + LeaseMargin;

    /// <summary>
    /// Gets how many rounds of concurrent sends one claimed batch is delivered in.
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
    /// <param name="kind">Email category whose limits are applied.</param>
    /// <returns>The resulting priority value, lowest first.</returns>
    public static int PriorityOf(EmailKind kind)
    {
        return kind switch
        {
            EmailKind.TwoFactorCode or EmailKind.PasswordReset or EmailKind.AccountVerification =>
                CriticalPriority,
            EmailKind.SecurityAlert or EmailKind.ActivityNotification => StandardPriority,
            _ => BulkPriority,
        };
    }

    /// <summary>
    /// Determines whether an email kind is accepted even when the outbox already holds
    /// <see cref="Capacity"/> messages. A full outbox, which an unreachable SMTP server and a few
    /// bulk mailings are enough to produce, must never stop a person from completing a login,
    /// resetting a password or verifying a new account; those kinds stay bounded by the per-recipient
    /// quota and the global budget of the email guard instead.
    /// </summary>
    /// <param name="kind">Email category whose limits are applied.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool IsCapacityExempt(EmailKind kind)
    {
        return PriorityOf(kind) == CriticalPriority;
    }

    /// <summary>
    /// Determines whether a message that has failed the supplied number of times is out of attempts
    /// and has to be discarded.
    /// </summary>
    /// <param name="attempts">Number of attempts that have already failed.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool IsExhausted(int attempts)
    {
        return attempts >= MaxAttempts;
    }

    /// <summary>
    /// Gets the delay that separates the supplied number of failed attempts from the next one.
    /// </summary>
    /// <param name="attempts">Number of attempts that have already failed.</param>
    /// <returns>The resulting time span value.</returns>
    public static TimeSpan RetryDelayAfter(int attempts)
    {
        var index = Math.Clamp(attempts - 1, 0, RetryDelays.Count - 1);
        return RetryDelays[index];
    }
}
