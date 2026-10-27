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
    /// <param name="error">Error of a failed result; <see langword="null"/> on success.</param>
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
    /// Gets the error of a failed result, or <see langword="null"/> when the operation succeeded.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Creates a successful result without a value.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Success()
    {
        return new Result(true, null);
    }

    /// <summary>
    /// Creates a successful result holding a value.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="value">Value the operation produced.</param>
    /// <returns>A successful result holding <paramref name="value"/>.</returns>
    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>
    /// Converts an error into a failed result.
    /// </summary>
    /// <param name="error">Error that names the failure.</param>
    /// <returns>A failed result carrying <paramref name="error"/>.</returns>
    public static implicit operator Result(Error error)
    {
        return new Result(false, error);
    }
}

/// <summary>
/// Represents the success or failure outcome of an application operation.
/// </summary>
/// <typeparam name="T">Type of the value a successful result holds.</typeparam>
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
    /// Gets the value produced by a successful result; reading it from a failed result throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    [AllowNull]
    public T Value =>
        IsSuccess
            ? field!
            : throw new InvalidOperationException("Cannot access the value of a failed result.");

    /// <summary>
    /// Converts a value into a successful result.
    /// </summary>
    /// <param name="value">Value the operation produced.</param>
    /// <returns>A successful result holding <paramref name="value"/>.</returns>
    public static implicit operator Result<T>(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>
    /// Converts an error into a failed result.
    /// </summary>
    /// <param name="error">Error that names the failure.</param>
    /// <returns>A failed result carrying <paramref name="error"/>.</returns>
    public static implicit operator Result<T>(Error error)
    {
        return new Result<T>(error);
    }
}
