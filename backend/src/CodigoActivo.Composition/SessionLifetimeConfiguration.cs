using System.Globalization;
using CodigoActivo.Application.Accounts;
using Microsoft.Extensions.Configuration;

namespace CodigoActivo.Composition;

/// <summary>
/// Maps <c>Auth:ExpireHours</c> to the lifetime of a session, shared by the stored session row and
/// the session cookie.
/// </summary>
public static class SessionLifetimeConfiguration
{
    /// <summary>
    /// Reads the configured session lifetime, falling back to the default when the value is
    /// absent or not a positive, representable number of hours.
    /// </summary>
    /// <param name="configuration">Application configuration to read.</param>
    /// <returns>The session lifetime options.</returns>
    public static SessionLifetimeOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = double.TryParse(
            configuration["Auth:ExpireHours"],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var hours
        );

        return new SessionLifetimeOptions
        {
            Lifetime =
                configured
                && double.IsFinite(hours)
                && hours > 0
                && hours < TimeSpan.MaxValue.TotalHours
                    ? TimeSpan.FromHours(hours)
                    : SessionLifetimeOptions.DefaultLifetime,
        };
    }
}
