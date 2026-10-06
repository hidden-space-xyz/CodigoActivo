using System.Diagnostics;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Common.Messaging;

/// <summary>
/// Refuses a command whose data annotations, or those of the messages it contains, are broken,
/// before its handler runs.
/// </summary>
/// <typeparam name="TCommand">Type of command handled.</typeparam>
/// <typeparam name="TResult">Result type of the command.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="validator">Validator of the annotations.</param>
public sealed class ValidationCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    MessageValidator validator
) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <inheritdoc />
    public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return validator.IsValid(command)
            ? inner.HandleAsync(command, ct)
            : Task.FromResult(
                ResultFailure.Of<TResult>(Error.Validation(ApplicationErrorCode.MessageInvalid))
            );
    }
}

/// <summary>
/// Refuses a query whose data annotations are broken, before its handler runs, when its result
/// can report the failure.
/// </summary>
/// <typeparam name="TQuery">Type of query handled.</typeparam>
/// <typeparam name="TResult">Result type of the query.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="validator">Validator of the annotations.</param>
public sealed class ValidationQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    MessageValidator validator
) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <inheritdoc />
    public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return !ResultFailure.IsSupported<TResult>() || validator.IsValid(query)
            ? inner.HandleAsync(query, ct)
            : Task.FromResult(
                ResultFailure.Of<TResult>(Error.Validation(ApplicationErrorCode.MessageInvalid))
            );
    }
}

/// <summary>
/// Commits what a command staged once its handler succeeds; the unit of work then publishes the
/// domain events of the commit. A failed command commits nothing more than its handler did.
/// </summary>
/// <typeparam name="TCommand">Type of command handled.</typeparam>
/// <typeparam name="TResult">Result type of the command.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="uow">Unit of work of the scope.</param>
public sealed class UnitOfWorkCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IUnitOfWork uow
) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        var result = await inner.HandleAsync(command, ct);
        if (result is Result { IsSuccess: true })
        {
            await uow.SaveChangesAsync(ct);
        }

        return result;
    }
}

/// <summary>
/// Warns when a command takes longer than <see cref="UseCaseTiming.SlowThreshold"/>.
/// </summary>
/// <typeparam name="TCommand">Type of command handled.</typeparam>
/// <typeparam name="TResult">Result type of the command.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="logger">Logger of slow use cases.</param>
public sealed class LoggingCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<LoggingCommandDecorator<TCommand, TResult>> logger
) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await inner.HandleAsync(command, ct);
        UseCaseTiming.WarnIfSlow(logger, typeof(TCommand), started);
        return result;
    }
}

/// <summary>
/// Warns when a query takes longer than <see cref="UseCaseTiming.SlowThreshold"/>.
/// </summary>
/// <typeparam name="TQuery">Type of query handled.</typeparam>
/// <typeparam name="TResult">Result type of the query.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="logger">Logger of slow use cases.</param>
public sealed class LoggingQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<LoggingQueryDecorator<TQuery, TResult>> logger
) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await inner.HandleAsync(query, ct);
        UseCaseTiming.WarnIfSlow(logger, typeof(TQuery), started);
        return result;
    }
}

/// <summary>
/// Measures how long a use case took.
/// </summary>
public static class UseCaseTiming
{
    /// <summary>
    /// Gets the duration above which a use case is logged as slow.
    /// </summary>
    public static TimeSpan SlowThreshold { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Logs a warning when the use case started at <paramref name="started"/> was slow.
    /// </summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="message">Type of the command or query.</param>
    /// <param name="started">Timestamp from <see cref="Stopwatch.GetTimestamp()"/> when it started.</param>
    public static void WarnIfSlow(ILogger logger, Type message, long started)
    {
        ArgumentNullException.ThrowIfNull(message);
        var elapsed = Stopwatch.GetElapsedTime(started);
        if (elapsed > SlowThreshold)
        {
            logger.SlowUseCase(message.Name, (long)elapsed.TotalMilliseconds);
        }
    }
}
