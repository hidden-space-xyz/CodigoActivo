using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// An event was created.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record EventCreated(EventId EventId) : IDomainEvent;

/// <summary>
/// The content, schedule, categories or terms of an event were replaced.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="ReleasedFileIds">Identifiers of the files the previous content referenced and the new one does not.</param>
public sealed record EventUpdated(
    EventId EventId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// An event was deleted together with its activities.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="ReleasedFileIds">Identifiers of the files the event and its activities referenced.</param>
public sealed record EventDeleted(
    EventId EventId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// An event started or stopped being the featured one.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Featured">Whether it is featured now.</param>
public sealed record EventFeaturedChanged(EventId EventId, bool Featured) : IDomainEvent;
