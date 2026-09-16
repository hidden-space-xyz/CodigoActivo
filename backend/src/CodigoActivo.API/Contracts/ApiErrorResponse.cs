using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Contracts;

/// <summary>
/// Contains the api error data returned by the API.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Status">The status value.</param>
/// <param name="Code">The code value.</param>
/// <param name="TraceId">The trace identifier value.</param>
public sealed record ApiErrorResponse(string Title, int Status, ErrorCode Code, string TraceId);
