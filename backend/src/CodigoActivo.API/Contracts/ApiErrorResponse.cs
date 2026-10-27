using CodigoActivo.API.Errors;

namespace CodigoActivo.API.Contracts;

/// <summary>
/// Body of every error response of the API.
/// </summary>
/// <param name="Title">English reason phrase of the status, such as <c>Not Found</c>.</param>
/// <param name="Status">HTTP status code of the response.</param>
/// <param name="Code">Wire code the client translates into the message it shows.</param>
/// <param name="TraceId">
/// Trace id of the request, the one the diagnostic and audit logs store, so a reported error can be
/// found.
/// </param>
public sealed record ApiErrorResponse(string Title, int Status, ErrorCode Code, string TraceId);
