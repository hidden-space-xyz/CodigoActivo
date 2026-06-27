namespace CodigoActivo.Domain.Common;

public enum ErrorKind
{
    BadRequest = 0,
    NotFound = 1,
    Forbidden = 2,
    Unauthorized = 3,
}

public sealed record Error(ErrorKind Kind)
{
    public static Error Validation()
    {
        return new(ErrorKind.BadRequest);
    }

    public static Error NotFound()
    {
        return new(ErrorKind.NotFound);
    }

    public static Error Forbidden()
    {
        return new(ErrorKind.Forbidden);
    }

    public static Error Unauthorized()
    {
        return new(ErrorKind.Unauthorized);
    }
}
