namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Defines configuration values for email queue.
/// </summary>
public sealed class EmailQueueOptions
{
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
    /// Gets or sets the capacity value.
    /// </summary>
    public int Capacity { get; set; } = DefaultCapacity;

    /// <summary>
    /// Gets or sets the workers value.
    /// </summary>
    public int Workers { get; set; } = DefaultWorkers;

    /// <summary>
    /// Gets or sets the shutdown drain value.
    /// </summary>
    public TimeSpan ShutdownDrain { get; set; } = DefaultShutdownDrain;

    /// <summary>
    /// Gets or sets the send timeout value.
    /// </summary>
    public TimeSpan SendTimeout { get; set; } = DefaultSendTimeout;
}
