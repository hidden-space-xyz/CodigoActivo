namespace CodigoActivo.API.Diagnostics;

/// <summary>
/// Declares the events the API layer records: the failures an operator has to act on, including the
/// one that ends the process. Request templates never carry the requested path, its query, the caller
/// address or any header, only the HTTP method and the route pattern of the matched endpoint.
/// </summary>
internal static partial class ApiLog
{
    /// <summary>
    /// Records an exception that reached the pipeline and was answered with a problem response.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="method">HTTP method of the request.</param>
    /// <param name="routeTemplate">Route pattern of the matched endpoint, never the requested path.</param>
    /// <param name="exception">Exception that was not handled by the endpoint.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {Method} {RouteTemplate}"
    )]
    internal static partial void UnhandledRequestException(
        this ILogger logger,
        string method,
        string routeTemplate,
        Exception exception
    );

    /// <summary>
    /// Records that signing out could not revoke the server-side session row, which leaves it
    /// usable until it expires.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while revoking the row.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to revoke the session row while signing out"
    )]
    internal static partial void SessionRevocationFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that signing out could not end the pending login challenge, which leaves it usable
    /// until it expires.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while ending the challenge.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to end the pending login challenge while signing out"
    )]
    internal static partial void ChallengeRevocationFailed(
        this ILogger logger,
        Exception exception
    );

    /// <summary>
    /// Records that the process is ending because starting or running the host failed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the process.</param>
    [LoggerMessage(Level = LogLevel.Critical, Message = "The API host terminated unexpectedly")]
    internal static partial void HostTerminatedUnexpectedly(
        this ILogger logger,
        Exception exception
    );
}
