using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public static class TestUnitOfWork
{
    public static UnitOfWork For(CodigoActivoDbContext context)
    {
        return new UnitOfWork(context, new NoListeners(), NullLogger<UnitOfWork>.Instance);
    }

    private sealed class NoListeners : IDomainEventPublisher
    {
        public Task PublishAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
