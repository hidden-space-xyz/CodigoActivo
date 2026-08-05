namespace CodigoActivo.Domain.Communication;

public sealed class EmailQueueOptions
{
    public const int DefaultCapacity = 1000;
    public const int DefaultWorkers = 4;
    public const int MaxWorkers = 16;

    public static readonly TimeSpan DefaultShutdownDrain = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan MaxShutdownDrain = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxSendTimeout = TimeSpan.FromMinutes(10);

    public int Capacity { get; set; } = DefaultCapacity;

    public int Workers { get; set; } = DefaultWorkers;

    public TimeSpan ShutdownDrain { get; set; } = DefaultShutdownDrain;

    public TimeSpan SendTimeout { get; set; } = DefaultSendTimeout;
}
