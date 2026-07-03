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
        return new(isSuccess: true, error: null);
    }

    public static Result<T> Success<T>(T value)
    {
        return new(value);
    }

    public static implicit operator Result(Error error) => new(isSuccess: false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? value;

    internal Result(T value)
        : base(isSuccess: true, error: null) => this.value = value;

    internal Result(Error error)
        : base(isSuccess: false, error) => this.value = default;

    public T Value =>
        IsSuccess
            ? value!
            : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
