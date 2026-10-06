using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Email an account logs in with and is reached at: trimmed and lowercase, with a single
/// <c>@</c> that is neither its first nor its last character and no line breaks.
/// </summary>
public sealed record EmailAddress
{
    private EmailAddress(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the normalized email.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Normalizes and checks a typed email.
    /// </summary>
    /// <param name="value">Email as typed.</param>
    /// <returns>The email, or <see cref="DomainErrorCode.UserEmailInvalid"/> when it is blank or malformed.</returns>
    public static Result<EmailAddress> Create(string? value)
    {
        var normalized = value.NormalizeEmailOrNull();
        if (normalized is null || normalized.Contains('\r') || normalized.Contains('\n'))
        {
            return Error.Validation(DomainErrorCode.UserEmailInvalid);
        }

        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at != normalized.Length - 1 && at == normalized.LastIndexOf('@')
            ? new EmailAddress(normalized)
            : Error.Validation(DomainErrorCode.UserEmailInvalid);
    }

    /// <summary>
    /// Restores an email that was checked before it was stored.
    /// </summary>
    /// <param name="value">Stored email.</param>
    /// <returns>The email.</returns>
    public static EmailAddress FromStored(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new EmailAddress(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
