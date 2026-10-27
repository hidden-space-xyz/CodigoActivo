namespace CodigoActivo.Application.Common.Caching;

/// <summary>
/// Names the tags that evict cached data, both application cache entries
/// (<see cref="ICachedQuery.Tags"/>) and HTTP output cache responses, whose policies are named
/// after the tags in <see cref="OutputCached"/>. <see cref="CacheTagsByEvent"/> says which domain
/// events invalidate each one.
/// </summary>
public static class CacheTags
{
    /// <summary>
    /// Tags the cached data of events.
    /// </summary>
    public const string Events = "events";

    /// <summary>
    /// Tags the cached data of event category types.
    /// </summary>
    public const string EventCategoryTypes = "event-category-types";

    /// <summary>
    /// Tags the cached data of news items.
    /// </summary>
    public const string News = "news";

    /// <summary>
    /// Tags the cached data of resources.
    /// </summary>
    public const string Resources = "resources";

    /// <summary>
    /// Tags the cached data of partners.
    /// </summary>
    public const string Partners = "partners";

    /// <summary>
    /// Tags the cached data of activities and their signups.
    /// </summary>
    public const string Activities = "activities";

    /// <summary>
    /// Tags the cached content of stored files.
    /// </summary>
    public const string Files = "files";

    /// <summary>
    /// Identifies the legal policies, whose version in force is output cached.
    /// </summary>
    public const string LegalPolicies = "legal-policies";

    /// <summary>
    /// Tags the cached data derived from accounts, such as the dashboard counts.
    /// </summary>
    public const string Users = "users";

    /// <summary>
    /// Tags the closed catalogs. No domain event invalidates it: they only change with a release.
    /// </summary>
    public const string Catalogs = "catalogs";

    /// <summary>
    /// Stores the tags whose invalidation evicts the dashboard summary.
    /// </summary>
    public static readonly IReadOnlyList<string> DashboardSummarySources =
    [
        Events,
        Activities,
        Resources,
        News,
        Partners,
        Users,
    ];

    /// <summary>
    /// Stores the tags whose invalidation evicts the dashboard analytics: those of the summary and
    /// the event category types.
    /// </summary>
    public static readonly IReadOnlyList<string> DashboardAnalyticsSources =
    [
        .. DashboardSummarySources,
        EventCategoryTypes,
    ];

    /// <summary>
    /// Stores the tags that each name an HTTP output cache policy, whose responses they evict.
    /// </summary>
    public static readonly IReadOnlyList<string> OutputCached =
    [
        Events,
        News,
        Resources,
        Partners,
        Activities,
        Files,
        LegalPolicies,
    ];

    /// <summary>
    /// Stores the tags invalidated when an account is erased: its user rows and participation, and
    /// every cached content type, whose author credits may pass to the initial administrator. Legal
    /// policies credit no author.
    /// </summary>
    public static readonly IReadOnlyList<string> Erasure =
    [
        .. OutputCached.Except([LegalPolicies]),
        Users,
    ];

    /// <summary>
    /// Stores every tag, whose invalidation evicts everything cached.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Events,
        EventCategoryTypes,
        News,
        Resources,
        Partners,
        Activities,
        Files,
        LegalPolicies,
        Users,
        Catalogs,
    ];
}
