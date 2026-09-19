namespace CodigoActivo.Application.Caching;

/// <summary>
/// Defines the shared cache tags used for cache and authorization configuration.
/// </summary>
public static class CacheTags
{
    /// <summary>
    /// Identifies the events configuration or policy value.
    /// </summary>
    public const string Events = "events";

    /// <summary>
    /// Identifies the event category types configuration or policy value.
    /// </summary>
    public const string EventCategoryTypes = "event-category-types";

    /// <summary>
    /// Identifies the announcements configuration or policy value.
    /// </summary>
    public const string Announcements = "announcements";

    /// <summary>
    /// Identifies the resources configuration or policy value.
    /// </summary>
    public const string Resources = "resources";

    /// <summary>
    /// Identifies the partners configuration or policy value.
    /// </summary>
    public const string Partners = "partners";

    /// <summary>
    /// Identifies the activities configuration or policy value.
    /// </summary>
    public const string Activities = "activities";

    /// <summary>
    /// Identifies the files configuration or policy value.
    /// </summary>
    public const string Files = "files";

    /// <summary>
    /// Identifies the users configuration or policy value.
    /// </summary>
    public const string Users = "users";

    /// <summary>
    /// Identifies the catalogs configuration or policy value.
    /// </summary>
    public const string Catalogs = "catalogs";

    /// <summary>
    /// Stores the sources used by dashboard counts.
    /// </summary>
    public static readonly IReadOnlyList<string> DashboardSummarySources =
    [
        Events,
        Activities,
        Resources,
        Announcements,
        Partners,
        Users,
    ];

    /// <summary>
    /// Stores the sources used by dashboard analytics.
    /// </summary>
    public static readonly IReadOnlyList<string> DashboardAnalyticsSources =
    [
        .. DashboardSummarySources,
        EventCategoryTypes,
    ];

    /// <summary>
    /// Stores the shared output cached value.
    /// </summary>
    public static readonly IReadOnlyList<string> OutputCached =
    [
        Events,
        Announcements,
        Resources,
        Partners,
        Activities,
        Files,
    ];

    /// <summary>
    /// Stores the shared all value.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Events,
        EventCategoryTypes,
        Announcements,
        Resources,
        Partners,
        Activities,
        Files,
        Users,
        Catalogs,
    ];
}
