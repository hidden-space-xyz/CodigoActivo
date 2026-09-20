namespace CodigoActivo.API.Diagnostics;

/// <summary>
/// Declares the events the API layer records: the process lifecycle and the failures an operator has
/// to act on. Request templates never carry the requested path, its query, the caller address or any
/// header, only the HTTP method and the route pattern of the matched endpoint.
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
    /// Records a rejected antiforgery token, which is also how a broken cookie or proxy setup shows.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Validation failure raised by the antiforgery service.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "CSRF validation failed")]
    internal static partial void CsrfValidationFailed(this ILogger logger, Exception exception);

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
    /// Records that signing out could not close the pending second-factor challenge.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while closing the challenge.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to close the pending challenge while signing out"
    )]
    internal static partial void PendingChallengeCloseFailed(
        this ILogger logger,
        Exception exception
    );

    /// <summary>
    /// Records that startup finished applying the database migrations.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    internal static partial void DatabaseMigrationsApplied(this ILogger logger);

    /// <summary>
    /// Records that startup finished seeding the catalogs.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Database seeding applied")]
    internal static partial void DatabaseSeedApplied(this ILogger logger);

    /// <summary>
    /// Records that the deployment mode was validated but not persisted, which only happens in a
    /// Development process running outside a container.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="configuredMode">Deployment mode the configuration selected.</param>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Deployment mode lock skipped: this is a Development process outside a container, "
            + "so the selected {ConfiguredMode} mode is not persisted"
    )]
    internal static partial void DeploymentModeLockSkipped(
        this ILogger logger,
        string configuredMode
    );

    /// <summary>
    /// Records that an expired log file could not be listed or removed, so it stays in the log
    /// directory until the next sweep. The path is left to the attached exception, which the
    /// formatter strips of identifiers before writing it.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while removing the file.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Expired log file removal failed")]
    internal static partial void LogFileRemovalFailed(this ILogger logger, Exception exception);

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
