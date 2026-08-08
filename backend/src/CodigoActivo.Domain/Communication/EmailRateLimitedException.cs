namespace CodigoActivo.Domain.Communication;

public enum EmailLimitScope
{
    None,
    Recipient,
    Global,
}

public enum EmailGuardAlert
{
    None,
    RecipientThrottled,
    GlobalBudgetLow,
    GlobalBudgetExhausted,
    TrackingSaturated,
}

public sealed class EmailRateLimitedException : Exception
{
    private const string DefaultMessage = "The outbound email quota denied this message.";

    public EmailRateLimitedException()
        : base(DefaultMessage) { }

    public EmailRateLimitedException(string message)
        : base(message) { }

    public EmailRateLimitedException(string message, Exception innerException)
        : base(message, innerException) { }

    public EmailRateLimitedException(EmailLimitScope scope)
        : base(DefaultMessage)
    {
        Scope = scope;
    }

    public EmailLimitScope Scope { get; }
}
