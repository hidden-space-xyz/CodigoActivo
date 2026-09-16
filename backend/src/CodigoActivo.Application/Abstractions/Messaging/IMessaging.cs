namespace CodigoActivo.Application.Abstractions.Messaging;

/// <summary>
/// Carries the criteria used to i.
/// </summary>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface IQuery<TResult>;

/// <summary>
/// Carries the input required to i.
/// </summary>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface ICommand<TResult>;

/// <summary>
/// Executes the query to i.
/// </summary>
/// <typeparam name="TQuery">Type of query handled by the component.</typeparam>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the request to i.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a t.</returns>
    public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}

/// <summary>
/// Executes the command to i.
/// </summary>
/// <typeparam name="TCommand">Type of command handled by the component.</typeparam>
/// <typeparam name="TResult">Type of result returned by the operation.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Handles the request to i.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a t.</returns>
    public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
