namespace CodigoActivo.Application.Abstractions.Messaging;

public interface IQuery<TResult>;

public interface ICommand<TResult>;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
