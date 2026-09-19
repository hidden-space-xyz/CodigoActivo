namespace CodigoActivo.Domain.Common;

/// <summary>
/// Identifies the supported error kind values.
/// </summary>
public enum ErrorKind
{
    /// <summary>
    /// Selects the bad request option.
    /// </summary>
    BadRequest = 0,

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
/// Represents an error value used by the application.
/// </summary>
/// <param name="Kind">Email category whose limits are applied.</param>
/// <param name="Code">The code value.</param>
public sealed record Error(ErrorKind Kind, ErrorCode Code)
{
    /// <summary>
    /// Creates an application error with the bad request classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error BadRequest(ErrorCode code)
    {
        return new Error(ErrorKind.BadRequest, code);
    }

    /// <summary>
    /// Creates an application error with the not found classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error NotFound(ErrorCode code)
    {
        return new Error(ErrorKind.NotFound, code);
    }

    /// <summary>
    /// Creates an application error with the forbidden classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Forbidden(ErrorCode code)
    {
        return new Error(ErrorKind.Forbidden, code);
    }

    /// <summary>
    /// Creates an application error with the unauthorized classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Unauthorized(ErrorCode code)
    {
        return new Error(ErrorKind.Unauthorized, code);
    }

    /// <summary>
    /// Creates an application error with the conflict classification.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The resulting error value.</returns>
    public static Error Conflict(ErrorCode code)
    {
        return new Error(ErrorKind.Conflict, code);
    }
}
