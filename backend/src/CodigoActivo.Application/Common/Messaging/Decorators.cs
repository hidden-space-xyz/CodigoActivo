using System.Diagnostics;
using System.Reflection;
using CodigoActivo.Application.Abstractions.Auditing;
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
    where TResult : Result
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
    where TResult : Result
{
    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        var result = await inner.HandleAsync(command, ct);
        if (result.IsSuccess)
        {
            await uow.SaveChangesAsync(ct);
        }

        return result;
    }
}

/// <summary>
/// Records the commands worth keeping in the audit log: who ran them and from where, what they
/// acted on and how they ended. Registered outside validation and the commit, it queues the entry
/// once both are over: a failed result, a concurrent update or a unique constraint violation is
/// recorded as rejected and any other exception as failed. The main thing is the one the handler
/// named through <see cref="IAuditSubject"/>, else the one a successful result created, else the
/// first typed identifier of the command; every other identifier it names is related. Auditing
/// never changes the outcome of a command. Commands marked <see cref="IUnaudited"/> are never
/// recorded, the success of a <see cref="IScheduledCommand"/> only when it changed something, a
/// <see cref="IPreparatoryCommand"/> never on success, the success of a
/// <see cref="IAccountLookupCommand"/> only when it found the account, and a
/// <see cref="IRoutineCommand"/> refused only for lack of permission. Every command, audited or
/// not, runs inside <see cref="ICommandScope"/>, so the data history names it.
/// </summary>
/// <typeparam name="TCommand">Type of command handled.</typeparam>
/// <typeparam name="TResult">Result type of the command.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="writer">Writer of the audit entries.</param>
/// <param name="subject">Main thing the handler named, if any.</param>
/// <param name="commandScope">Name of the running command for the data history.</param>
public sealed class AuditCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    AuditWriter writer,
    IAuditSubject subject,
    ICommandScope commandScope
) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : Result
{
    private const string ConcurrentUpdate = "ConcurrentUpdate";
    private const string UniqueConstraintViolation = "UniqueConstraintViolation";

    private static readonly string CommandName = MessageAudit.NameOf(typeof(TCommand));

    private static readonly AuditAction? Action = typeof(IUnaudited).IsAssignableFrom(
        typeof(TCommand)
    )
        ? null
        : MessageAudit.ActionOf(typeof(TCommand));

    private static readonly bool Scheduled = typeof(IScheduledCommand).IsAssignableFrom(
        typeof(TCommand)
    );

    private static readonly bool Preparatory = typeof(IPreparatoryCommand).IsAssignableFrom(
        typeof(TCommand)
    );

    private static readonly bool AccountLookup = typeof(IAccountLookupCommand).IsAssignableFrom(
        typeof(TCommand)
    );

    private static readonly bool Routine = typeof(IRoutineCommand).IsAssignableFrom(
        typeof(TCommand)
    );

    private static readonly PropertyInfo? ResultValue = typeof(TResult).IsGenericType
        ? typeof(TResult).GetProperty(nameof(Result<object>.Value))
        : null;

    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var running = commandScope.Enter(CommandName);
        if (Action is not { } action)
        {
            return await inner.HandleAsync(command, ct);
        }

        subject.Clear();
        try
        {
            var result = await inner.HandleAsync(command, ct);
            Record(
                action,
                command,
                result,
                result.IsSuccess ? AuditOutcome.Succeeded : AuditOutcome.Rejected,
                result.Error?.Code.ToString()
            );
            return result;
        }
        catch (ConcurrentUpdateException)
        {
            Record(action, command, null, AuditOutcome.Rejected, ConcurrentUpdate);
            throw;
        }
        catch (UniqueConstraintViolationException)
        {
            Record(action, command, null, AuditOutcome.Rejected, UniqueConstraintViolation);
            throw;
        }
        catch (Exception)
        {
            Record(action, command, null, AuditOutcome.Failed, errorCode: null);
            throw;
        }
        finally
        {
            subject.Clear();
        }
    }

    private bool IsWorthRecording(AuditOutcome outcome, ErrorKind? refusal, object? value)
    {
        return outcome switch
        {
            AuditOutcome.Succeeded => !Preparatory
                && !(Scheduled && value is 0)
                && !(AccountLookup && subject.Entity is null),
            AuditOutcome.Rejected => !Routine || refusal is ErrorKind.Forbidden,
            _ => true,
        };
    }

    private void Record(
        AuditAction action,
        TCommand command,
        TResult? result,
        AuditOutcome outcome,
        string? errorCode
    )
    {
        var value = result is { IsSuccess: true } ? ResultValue?.GetValue(result) : null;
        if (IsWorthRecording(outcome, result?.Error?.Kind, value))
        {
            writer.Write(
                new AuditDraft(action, outcome)
                {
                    Message = command,
                    Subject = subject.Entity,
                    ResultValue = value,
                    ErrorCode = errorCode,
                    Scheduled = Scheduled,
                }
            );
        }
    }
}

/// <summary>
/// Records the reads marked <see cref="IAuditedQuery"/> that handed their data over; every other
/// query goes through untouched. Registered outside the cache, so a cached answer is recorded too.
/// </summary>
/// <typeparam name="TQuery">Type of query handled.</typeparam>
/// <typeparam name="TResult">Result type of the query.</typeparam>
/// <param name="inner">Decorated handler.</param>
/// <param name="writer">Writer of the audit entries.</param>
public sealed class AuditQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    AuditWriter writer
) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private static readonly AuditAction? Action = typeof(IAuditedQuery).IsAssignableFrom(
        typeof(TQuery)
    )
        ? MessageAudit.ActionOf(typeof(TQuery))
        : null;

    /// <inheritdoc />
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await inner.HandleAsync(query, ct);
        if (Action is { } action && result is Result { IsSuccess: true })
        {
            writer.Write(new AuditDraft(action, AuditOutcome.Succeeded) { Message = query });
        }

        return result;
    }
}

/// <summary>
/// Holds the main thing the running command named for the audit log, for one scope.
/// </summary>
public sealed class AuditSubject : IAuditSubject
{
    /// <inheritdoc />
    public AuditEntity? Entity { get; private set; }

    /// <inheritdoc />
    public void Name<TId>(TId id)
        where TId : struct, IEntityId<TId>
    {
        Entity = AuditEntity.Of(id);
    }

    /// <inheritdoc />
    public void Clear()
    {
        Entity = null;
    }
}

/// <summary>
/// Holds the name of the running command for one scope.
/// </summary>
public sealed class CommandScope : ICommandScope
{
    /// <inheritdoc />
    public string? Command { get; private set; }

    /// <inheritdoc />
    public IDisposable Enter(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var previous = Command;
        Command = command;
        return new Exit(this, previous);
    }

    private sealed class Exit(CommandScope scope, string? previous) : IDisposable
    {
        public void Dispose()
        {
            scope.Command = previous;
        }
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
    where TResult : Result
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
    /// Gets the duration above which a use case is logged as slow. Hashing a password can take
    /// about a second on modest hardware, so only a wait a person would take for a failure is
    /// logged.
    /// </summary>
    public static TimeSpan SlowThreshold { get; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Logs a warning when the use case started at <paramref name="started"/> was slow.
    /// </summary>
    /// <param name="logger">Logger to write to.</param>
    /// <param name="message">Type of the command or query.</param>
    /// <param name="started">
    /// Timestamp from <see cref="Stopwatch.GetTimestamp()"/> when it started.
    /// </param>
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
