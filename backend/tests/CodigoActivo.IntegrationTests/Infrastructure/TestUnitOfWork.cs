using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public static class TestUnitOfWork
{
    public static UnitOfWork For(CodigoActivoDbContext context)
    {
        return new UnitOfWork(context, new NoListeners());
    }

    private sealed class NoListeners : IDomainEventPublisher
    {
        public Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents)
        {
            return Task.CompletedTask;
        }
    }
}
