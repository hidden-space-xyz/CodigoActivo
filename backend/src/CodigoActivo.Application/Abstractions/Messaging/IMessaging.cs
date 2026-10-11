using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Abstractions.Messaging;

/// <summary>
/// Marks the criteria of a use case that only reads.
/// </summary>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface IQuery<TResult>;

/// <summary>
/// Marks the input of a use case that changes state. It always answers a <see cref="Result"/>,
/// so the decorators can refuse it with a failure and commit it only when it succeeds.
/// </summary>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface ICommand<TResult>
    where TResult : Result;

/// <summary>
/// Runs one query.
/// </summary>
/// <typeparam name="TQuery">Type of query handled by the component.</typeparam>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Reads what the query asks for.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains what the query reads.</returns>
    public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}

/// <summary>
/// Runs one command.
/// </summary>
/// <typeparam name="TCommand">Type of command handled by the component.</typeparam>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : Result
{
    /// <summary>
    /// Applies the command.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or the failure that refused the command.</returns>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
