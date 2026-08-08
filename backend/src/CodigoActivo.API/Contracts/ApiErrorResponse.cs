using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Contracts;

public sealed record ApiErrorResponse(string Title, int Status, ErrorCode Code, string TraceId);
