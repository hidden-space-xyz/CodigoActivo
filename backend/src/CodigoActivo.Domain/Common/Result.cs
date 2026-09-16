using System.Diagnostics.CodeAnalysis;

namespace CodigoActivo.Domain.Common;

/// <summary>
/// Represents the success or failure outcome of an application operation.
/// </summary>
public class Result
{
    /// <summary>
    /// Initializes an operation result with its success state and optional error.
    /// </summary>
    /// <param name="isSuccess">Whether the operation completed successfully.</param>
    /// <param name="error">Application error associated with a failed result or response.</param>
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the application error produced by a failed result.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Creates a successful result, optionally containing a value.
    /// </summary>
    /// <returns>The application operation outcome.</returns>
    public static Result Success()
    {
        return new Result(true, null);
    }

    /// <summary>
    /// Creates a successful result, optionally containing a value.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="value">Value to validate or convert.</param>
    /// <returns>The application operation outcome.</returns>
    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>
    /// Converts the source value to the declared target type.
    /// </summary>
    /// <param name="error">Application error associated with a failed result or response.</param>
    /// <returns>The application operation outcome.</returns>
    public static implicit operator Result(Error error)
    {
        return new Result(false, error);
    }
}

/// <summary>
/// Represents the success or failure outcome of an application operation.
/// </summary>
/// <typeparam name="T">Type of item processed by the operation.</typeparam>
public sealed class Result<T> : Result
{
    internal Result(T value)
        : base(true, null)
    {
        Value = value;
    }

    internal Result(Error error)
        : base(false, error)
    {
        Value = default;
    }

    /// <summary>
    /// Gets the value produced by a successful result.
    /// </summary>
    [AllowNull]
    public T Value =>
        IsSuccess
            ? field!
            : throw new InvalidOperationException("Cannot access the value of a failed result.");

    /// <summary>
    /// Converts the source value to the declared target type.
    /// </summary>
    /// <param name="value">Value to validate or convert.</param>
    /// <returns>The application operation outcome.</returns>
    public static implicit operator Result<T>(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>
    /// Converts the source value to the declared target type.
    /// </summary>
    /// <param name="error">Application error associated with a failed result or response.</param>
    /// <returns>The application operation outcome.</returns>
    public static implicit operator Result<T>(Error error)
    {
        return new Result<T>(error);
    }
}
