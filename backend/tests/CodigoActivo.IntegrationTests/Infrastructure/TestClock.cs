using CodigoActivo.Domain.Common;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministic <see cref="IClock"/> for the integration host. Fixed to 2026-07-04 so date-boundary
/// reads (upcoming vs past events, OTP expiry) are reproducible; tests that need a different "today"
/// mutate the properties before acting.
/// </summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    public DateOnly Today { get; set; } = new(2026, 7, 4);
}
