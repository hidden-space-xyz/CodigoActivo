namespace CodigoActivo.Domain.Security;

/// <summary>
/// Generates shared secrets and matches time-based one-time passwords (RFC 6238).
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Creates a new random shared secret encoded in Base32 without padding.
    /// </summary>
    /// <returns>The generated secret.</returns>
    public string GenerateSecret();

    /// <summary>
    /// Finds the time step, within the accepted clock drift, whose code equals the supplied one.
    /// </summary>
    /// <param name="secret">Base32 shared secret of the authenticator.</param>
    /// <param name="code">Code typed by the user.</param>
    /// <param name="now">Current timestamp used to compute the candidate time steps.</param>
    /// <returns>The matching time step, or <see langword="null"/> when no candidate matches.</returns>
    public long? MatchStep(string secret, string code, DateTimeOffset now);
}
