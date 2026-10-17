namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Names the kind of thing an audit entry points at: one member per typed identifier, named after
/// it without the <c>Id</c> suffix. Sessions, login challenges and event ratings are never
/// recorded: the first two are credentials and a rating is anonymous. Entries store the member
/// name, so members are only ever added.
/// </summary>
public enum AuditEntityType
{
    /// <summary>An activity.</summary>
    Activity,

    /// <summary>An event.</summary>
    Event,

    /// <summary>An event category.</summary>
    EventCategoryType,

    /// <summary>A news item.</summary>
    NewsItem,

    /// <summary>A partner.</summary>
    Partner,

    /// <summary>A resource.</summary>
    Resource,

    /// <summary>A stored file.</summary>
    StoredFile,

    /// <summary>A terms document.</summary>
    TermsDocument,

    /// <summary>A user.</summary>
    User,
}
