namespace CodigoActivo.Domain.Common;

public enum ErrorKind
{
    BadRequest = 0,
    NotFound = 1,
    Forbidden = 2,
    Unauthorized = 3,
    Conflict = 4,
}

public sealed record Error(ErrorKind Kind, ErrorCode Code)
{
    public static Error BadRequest(ErrorCode code)
    {
        return new Error(ErrorKind.BadRequest, code);
    }

    public static Error NotFound(ErrorCode code)
    {
        return new Error(ErrorKind.NotFound, code);
    }

    public static Error Forbidden(ErrorCode code)
    {
        return new Error(ErrorKind.Forbidden, code);
    }

    public static Error Unauthorized(ErrorCode code)
    {
        return new Error(ErrorKind.Unauthorized, code);
    }

    public static Error Conflict(ErrorCode code)
    {
        return new Error(ErrorKind.Conflict, code);
    }
}