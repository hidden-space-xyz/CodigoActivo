using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Errors;

/// <summary>
/// Failure the API reports: its kind, which picks the HTTP status, and its wire code.
/// </summary>
/// <param name="Kind">Kind of failure.</param>
/// <param name="Code">Code sent to the client.</param>
public sealed record ApiError(ErrorKind Kind, ErrorCode Code)
{
    /// <summary>
    /// Translates a failure reported by a use case into its wire form.
    /// </summary>
    /// <param name="error">Failure reported by the domain or the application.</param>
    /// <returns>The failure with its wire code.</returns>
    public static ApiError From(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ApiError(error.Kind, WireErrorCodes.ToWire(error.Code));
    }

    /// <summary>
    /// Creates a failure of a request the API refuses as invalid.
    /// </summary>
    /// <param name="code">Code sent to the client.</param>
    /// <returns>The failure.</returns>
    public static ApiError Validation(ErrorCode code)
    {
        return new ApiError(ErrorKind.Validation, code);
    }

    /// <summary>
    /// Creates a failure of a request for something that does not exist.
    /// </summary>
    /// <param name="code">Code sent to the client.</param>
    /// <returns>The failure.</returns>
    public static ApiError NotFound(ErrorCode code)
    {
        return new ApiError(ErrorKind.NotFound, code);
    }

    /// <summary>
    /// Creates a failure of a request the caller may not make.
    /// </summary>
    /// <param name="code">Code sent to the client.</param>
    /// <returns>The failure.</returns>
    public static ApiError Forbidden(ErrorCode code)
    {
        return new ApiError(ErrorKind.Forbidden, code);
    }

    /// <summary>
    /// Creates a failure of a request without a valid identity.
    /// </summary>
    /// <param name="code">Code sent to the client.</param>
    /// <returns>The failure.</returns>
    public static ApiError Unauthorized(ErrorCode code)
    {
        return new ApiError(ErrorKind.Unauthorized, code);
    }
}
