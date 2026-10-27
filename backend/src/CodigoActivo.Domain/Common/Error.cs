namespace CodigoActivo.Domain.Common;

/// <summary>
/// Classifies an expected failure; the API answers each kind with its own HTTP status.
/// </summary>
public enum ErrorKind
{
    /// <summary>
    /// The request breaks a business rule or carries invalid data.
    /// </summary>
    Validation = 0,

    /// <summary>
    /// Something the request names does not exist, or is not visible to the caller.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The caller is known but may not perform the operation.
    /// </summary>
    Forbidden = 2,

    /// <summary>
    /// The caller is not authenticated, such as after wrong credentials, with an expired login
    /// challenge or for an account that no longer exists.
    /// </summary>
    Unauthorized = 3,

    /// <summary>
    /// The operation clashes with the current state, such as a duplicate or a state that does not
    /// allow it.
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
    /// Creates a domain or application error with the validation classification.
    /// </summary>
    /// <param name="code">Member of the error code enumeration that names the failure.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Validation(Enum code)
    {
        return new Error(ErrorKind.Validation, code);
    }

    /// <summary>
    /// Creates a domain or application error with the not found classification.
    /// </summary>
    /// <param name="code">Member of the error code enumeration that names the failure.</param>
    /// <returns>The resulting error value.</returns>
    public static Error NotFound(Enum code)
    {
        return new Error(ErrorKind.NotFound, code);
    }

    /// <summary>
    /// Creates a domain or application error with the forbidden classification.
    /// </summary>
    /// <param name="code">Member of the error code enumeration that names the failure.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Forbidden(Enum code)
    {
        return new Error(ErrorKind.Forbidden, code);
    }

    /// <summary>
    /// Creates a domain or application error with the unauthorized classification.
    /// </summary>
    /// <param name="code">Member of the error code enumeration that names the failure.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Unauthorized(Enum code)
    {
        return new Error(ErrorKind.Unauthorized, code);
    }

    /// <summary>
    /// Creates a domain or application error with the conflict classification.
    /// </summary>
    /// <param name="code">Member of the error code enumeration that names the failure.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Conflict(Enum code)
    {
        return new Error(ErrorKind.Conflict, code);
    }
}
