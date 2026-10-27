namespace CodigoActivo.Application.Abstractions.Email;

/// <summary>
/// Names the sending limit that refused an email.
/// </summary>
public enum EmailLimitScope
{
    /// <summary>
    /// No limit is named.
    /// </summary>
    None,

    /// <summary>
    /// The hourly or daily quota of the addressee.
    /// </summary>
    Recipient,

    /// <summary>
    /// The budget the whole application shares, or the capacity of a full outbox.
    /// </summary>
    Global,
}

/// <summary>
/// Represents an email a sending limit refused, so it was not queued. Callers decide whether that
/// fails their request.
/// </summary>
public sealed class EmailRateLimitedException : Exception
{
    private const string DefaultMessage = "The outbound email quota denied this message.";

    /// <summary>
    /// Initializes a refusal without naming the limit.
    /// </summary>
    public EmailRateLimitedException()
        : base(DefaultMessage) { }

    /// <summary>
    /// Initializes a refusal with a diagnostic message.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    public EmailRateLimitedException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a refusal with a diagnostic message and its cause.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    /// <param name="innerException">Underlying exception that caused the failure.</param>
    public EmailRateLimitedException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a refusal by a known limit.
    /// </summary>
    /// <param name="scope">Limit that refused the email.</param>
    public EmailRateLimitedException(EmailLimitScope scope)
        : base(DefaultMessage)
    {
        Scope = scope;
    }

    /// <summary>
    /// Gets the limit that refused the email; <see cref="EmailLimitScope.None"/> when it was not named.
    /// </summary>
    public EmailLimitScope Scope { get; }
}
