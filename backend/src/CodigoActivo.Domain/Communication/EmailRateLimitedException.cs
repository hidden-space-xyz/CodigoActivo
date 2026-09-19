namespace CodigoActivo.Domain.Communication;

/// <summary>
/// Identifies the supported email limit scope values.
/// </summary>
public enum EmailLimitScope
{
    /// <summary>
    /// No option is selected.
    /// </summary>
    None,

    /// <summary>
    /// Selects the recipient option.
    /// </summary>
    Recipient,

    /// <summary>
    /// Selects the global option.
    /// </summary>
    Global,
}

/// <summary>
/// Identifies the supported email guard alert values.
/// </summary>
public enum EmailGuardAlert
{
    /// <summary>
    /// No option is selected.
    /// </summary>
    None,

    /// <summary>
    /// Selects the recipient throttled option.
    /// </summary>
    RecipientThrottled,

    /// <summary>
    /// Selects the global budget low option.
    /// </summary>
    GlobalBudgetLow,

    /// <summary>
    /// Selects the global budget exhausted option.
    /// </summary>
    GlobalBudgetExhausted,

    /// <summary>
    /// Selects the tracking saturated option.
    /// </summary>
    TrackingSaturated,
}

/// <summary>
/// Represents a failure caused by email rate limited.
/// </summary>
public sealed class EmailRateLimitedException : Exception
{
    private const string DefaultMessage = "The outbound email quota denied this message.";

    /// <summary>
    /// Initializes an email rate limited exception with its required dependencies.
    /// </summary>
    public EmailRateLimitedException()
        : base(DefaultMessage) { }

    /// <summary>
    /// Initializes an email rate limited exception with its required dependencies.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    public EmailRateLimitedException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes an email rate limited exception with its required dependencies.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="innerException">Underlying exception that caused the failure.</param>
    public EmailRateLimitedException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>
    /// Initializes an email rate limited exception with its required dependencies.
    /// </summary>
    /// <param name="scope">The scope value.</param>
    public EmailRateLimitedException(EmailLimitScope scope)
        : base(DefaultMessage)
    {
        Scope = scope;
    }

    /// <summary>
    /// Gets the scope value.
    /// </summary>
    public EmailLimitScope Scope { get; }
}
