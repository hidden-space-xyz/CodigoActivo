using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.UnitTests.TestSupport;

internal static class DomainEvents
{
    public static IReadOnlyCollection<StoredFileId> ReleasedFiles(IHasDomainEvents aggregate)
    {
        return
        [
            .. aggregate
                .PullDomainEvents()
                .OfType<IReleasesFiles>()
                .SelectMany(domainEvent => domainEvent.ReleasedFileIds)
                .Distinct(),
        ];
    }

    public static IReadOnlyList<TEvent> Raised<TEvent>(IHasDomainEvents aggregate)
        where TEvent : IDomainEvent
    {
        return [.. aggregate.PullDomainEvents().OfType<TEvent>()];
    }
}
