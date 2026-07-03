namespace CodigoActivo.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success()
    {
        return new Result(true, null);
    }

    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value);
    }

    public static implicit operator Result(Error error)
    {
        return new Result(false, error);
    }
}

public sealed class Result<T> : Result
{
    private readonly T? value;

    internal Result(T value)
        : base(true, null)
    {
        this.value = value;
    }

    internal Result(Error error)
        : base(false, error)
    {
        this.value = default;
    }

    public T Value =>
        IsSuccess
            ? value!
            : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<T>(T value)
    {
        return new Result<T>(value);
    }

    public static implicit operator Result<T>(Error error)
    {
        return new Result<T>(error);
    }
}