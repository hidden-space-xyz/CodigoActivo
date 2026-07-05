namespace CodigoActivo.Domain.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today { get; }

    TimeZoneInfo TimeZone { get; }
}
