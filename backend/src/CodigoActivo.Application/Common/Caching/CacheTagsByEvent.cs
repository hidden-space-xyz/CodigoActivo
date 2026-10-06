using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Caching;

/// <summary>
/// Cache tags each domain event makes stale. Events without an entry change no cached data.
/// </summary>
public static class CacheTagsByEvent
{
    private static readonly Dictionary<Type, string[]> Tags = new()
    {
        [typeof(PartnerCreated)] = [CacheTags.Partners],
        [typeof(PartnerUpdated)] = [CacheTags.Partners],
        [typeof(PartnerDeleted)] = [CacheTags.Partners],
        [typeof(NewsItemCreated)] = [CacheTags.News],
        [typeof(NewsItemUpdated)] = [CacheTags.News],
        [typeof(NewsItemDeleted)] = [CacheTags.News],
        [typeof(NewsItemFeaturedChanged)] = [CacheTags.News],
        [typeof(ResourceCreated)] = [CacheTags.Resources],
        [typeof(ResourceUpdated)] = [CacheTags.Resources],
        [typeof(ResourceDeleted)] = [CacheTags.Resources],
        [typeof(StoredFileReplaced)] = [CacheTags.Files],
        [typeof(StoredFileDeleted)] = [CacheTags.Files],
        [typeof(EventCategoryTypeCreated)] = [CacheTags.EventCategoryTypes],
        [typeof(EventCategoryTypeRenamed)] = [CacheTags.EventCategoryTypes, CacheTags.Events],
        [typeof(EventCategoryTypeDeleted)] = [CacheTags.EventCategoryTypes, CacheTags.Events],
        [typeof(TermsDocumentRewritten)] = [CacheTags.Events],
        [typeof(EventCreated)] = [CacheTags.Events],
        [typeof(EventUpdated)] = [CacheTags.Events],
        [typeof(EventDeleted)] = [CacheTags.Events, CacheTags.Activities],
        [typeof(EventFeaturedChanged)] = [CacheTags.Events],
        [typeof(ActivityCreated)] = [CacheTags.Activities],
        [typeof(ActivityUpdated)] = [CacheTags.Activities],
        [typeof(ActivityDeleted)] = [CacheTags.Activities],
        [typeof(AssignmentRequested)] = [CacheTags.Activities],
        [typeof(AssignmentWithdrawn)] = [CacheTags.Activities],
        [typeof(AssignmentRoleChanged)] = [CacheTags.Activities],
        [typeof(AssignmentStatusChanged)] = [CacheTags.Activities],
        [typeof(AccountRegistered)] = [CacheTags.Users],
        [typeof(DependentAdded)] = [CacheTags.Users],
        [typeof(ProfileChanged)] = [CacheTags.Users],
        [typeof(UserTypeChanged)] = [CacheTags.Users],
        [typeof(AdministratorRightsChanged)] = [CacheTags.Users],
        [typeof(AccountErased)] = [.. CacheTags.Erasure],
    };

    /// <summary>
    /// Gets the tags an event makes stale.
    /// </summary>
    /// <param name="domainEvent">Committed event.</param>
    /// <returns>The tags to invalidate, empty when the event changes no cached data.</returns>
    public static IReadOnlyList<string> For(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return Tags.TryGetValue(domainEvent.GetType(), out var tags) ? tags : [];
    }

    /// <summary>
    /// Gets the event types that make cached data stale.
    /// </summary>
    public static IReadOnlyCollection<Type> MappedEvents => Tags.Keys;
}
