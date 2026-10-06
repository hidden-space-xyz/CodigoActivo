namespace CodigoActivo.Domain.Common;

/// <summary>
/// Identifies the supported error kind values.
/// </summary>
public enum ErrorKind
{
    /// <summary>
    /// The request breaks a business rule or carries invalid data.
    /// </summary>
    Validation = 0,

    /// <summary>
    /// Selects the not found option.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Selects the forbidden option.
    /// </summary>
    Forbidden = 2,

    /// <summary>
    /// Selects the unauthorized option.
    /// </summary>
    Unauthorized = 3,

    /// <summary>
    /// Selects the conflict option.
    /// </summary>
    Conflict = 4,
}

/// <summary>
/// Represents an expected failure: its kind and the code that names it, a <see cref="DomainErrorCode"/>
/// for a broken domain rule or a code of the layer that reported the failure.
/// </summary>
/// <param name="Kind">Kind of failure.</param>
/// <param name="Code">Member of the error code enumeration that names the failure.</param>
public sealed record Error(ErrorKind Kind, Enum Code)
{
    /// <summary>
    /// Creates an application error with the validation classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Validation(Enum code)
    {
        return new Error(ErrorKind.Validation, code);
    }

    /// <summary>
    /// Creates an application error with the not found classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error NotFound(Enum code)
    {
        return new Error(ErrorKind.NotFound, code);
    }

    /// <summary>
    /// Creates an application error with the forbidden classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Forbidden(Enum code)
    {
        return new Error(ErrorKind.Forbidden, code);
    }

    /// <summary>
    /// Creates an application error with the unauthorized classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Unauthorized(Enum code)
    {
        return new Error(ErrorKind.Unauthorized, code);
    }

    /// <summary>
    /// Creates an application error with the conflict classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Conflict(Enum code)
    {
        return new Error(ErrorKind.Conflict, code);
    }
}
