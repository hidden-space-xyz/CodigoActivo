namespace CodigoActivo.Application.Caching;

public static class CacheTags
{
    public const string Events = "events";
    public const string EventCategoryTypes = "event-category-types";
    public const string Announcements = "announcements";
    public const string Resources = "resources";
    public const string Partners = "partners";
    public const string Activities = "activities";
    public const string Files = "files";
    public const string Users = "users";
    public const string Catalogs = "catalogs";

    public static readonly IReadOnlyList<string> OutputCached =
    [
        Events,
        Announcements,
        Resources,
        Partners,
        Activities,
        Files,
    ];

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
