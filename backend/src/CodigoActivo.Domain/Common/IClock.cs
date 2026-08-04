namespace CodigoActivo.Domain.Common;

public interface IClock
{
    public DateTimeOffset UtcNow { get; }

    public DateOnly Today { get; }

    public TimeZoneInfo TimeZone { get; }
}
