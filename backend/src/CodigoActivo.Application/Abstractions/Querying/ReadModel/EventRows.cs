namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Stored event as the queries read it.
/// </summary>
public sealed class EventRow
{
    /// <summary>Gets the identifier of the event.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the subtitle.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>Gets the rich-text description.</summary>
    public string Description { get; init; } = "{}";

    /// <summary>Gets the first day of the event.</summary>
    public DateOnly EventStartsAt { get; init; }

    /// <summary>Gets the last day of the event.</summary>
    public DateOnly EventEndsAt { get; init; }

    /// <summary>Gets when the early signup opens, if there is one.</summary>
    public DateTimeOffset? EarlySignupStartsAt { get; init; }

    /// <summary>Gets when the signup opens.</summary>
    public DateTimeOffset SignupStartsAt { get; init; }

    /// <summary>Gets when the signup closes.</summary>
    public DateTimeOffset SignupEndsAt { get; init; }

    /// <summary>Gets a value indicating whether the event is featured.</summary>
    public bool Featured { get; init; }

    /// <summary>Gets the identifier of the thumbnail file.</summary>
    public Guid ThumbnailId { get; init; }

    /// <summary>Gets when the event was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the event was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Gets the identifier of the author.</summary>
    public Guid CreatedBy { get; init; }

    /// <summary>Gets the identifier of the last editor.</summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>Gets the terms documents linked to the event.</summary>
    public ICollection<EventTermsDocumentRow> TermsDocuments { get; init; } = [];

    /// <summary>Gets the activities of the event.</summary>
    public ICollection<ActivityRow> Activities { get; init; } = [];

    /// <summary>Gets the categories of the event.</summary>
    public ICollection<EventCategoryRow> Categories { get; init; } = [];

    /// <summary>Gets the anonymous ratings of the event.</summary>
    public ICollection<EventRatingRow> Ratings { get; init; } = [];
}

/// <summary>
/// Stored anonymous event rating as the queries read it.
/// </summary>
public sealed class EventRatingRow
{
    /// <summary>Gets the identifier of the rating.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the rated event.</summary>
    public Guid EventId { get; init; }

    /// <summary>Gets the rated event.</summary>
    public EventRow Event { get; init; } = null!;

    /// <summary>Gets the score, or <see langword="null"/> when the participant gave none.</summary>
    public int? Score { get; init; }

    /// <summary>Gets what the participant liked most.</summary>
    public string? MostLiked { get; init; }

    /// <summary>Gets what the participant liked least.</summary>
    public string? LeastLiked { get; init; }

    /// <summary>Gets the suggestions.</summary>
    public string? Suggestions { get; init; }
}

/// <summary>
/// Stored category tag of an event as the queries read it.
/// </summary>
public sealed class EventCategoryRow
{
    /// <summary>Gets the identifier of the event.</summary>
    public Guid EventId { get; init; }

    /// <summary>Gets the event.</summary>
    public EventRow Event { get; init; } = null!;

    /// <summary>Gets the identifier of the category type.</summary>
    public Guid EventCategoryTypeId { get; init; }

    /// <summary>Gets the category type.</summary>
    public EventCategoryTypeRow EventCategoryType { get; init; } = null!;
}

/// <summary>
/// Stored event category type as the queries read it.
/// </summary>
public sealed class EventCategoryTypeRow
{
    /// <summary>Gets the identifier of the category type.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the display color.</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Gets the events tagged with the category type.</summary>
    public ICollection<EventCategoryRow> Events { get; init; } = [];
}

/// <summary>
/// Stored link between an event and a terms document as the queries read it.
/// </summary>
public sealed class EventTermsDocumentRow
{
    /// <summary>Gets the identifier of the event.</summary>
    public Guid EventId { get; init; }

    /// <summary>Gets the event.</summary>
    public EventRow Event { get; init; } = null!;

    /// <summary>Gets the identifier of the terms document.</summary>
    public Guid TermsDocumentId { get; init; }

    /// <summary>Gets the terms document.</summary>
    public TermsDocumentRow TermsDocument { get; init; } = null!;

    /// <summary>Gets a value indicating whether accepting the document is required to sign up.</summary>
    public bool IsRequired { get; init; }

    /// <summary>Gets the position of the document in the event.</summary>
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Stored decision about a terms document of an event for a person as the queries read it.
/// </summary>
public sealed class EventTermsAcceptanceRow
{
    /// <summary>Gets the identifier of the event.</summary>
    public Guid EventId { get; init; }

    /// <summary>Gets the event.</summary>
    public EventRow Event { get; init; } = null!;

    /// <summary>Gets the identifier of the person the decision is about.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the person the decision is about.</summary>
    public UserRow User { get; init; } = null!;

    /// <summary>Gets the identifier of the person who decided.</summary>
    public Guid DecidedById { get; init; }

    /// <summary>Gets the person who decided: the person or their guardian.</summary>
    public UserRow DecidedBy { get; init; } = null!;

    /// <summary>Gets the identifier of the terms document.</summary>
    public Guid TermsDocumentId { get; init; }

    /// <summary>Gets the terms document.</summary>
    public TermsDocumentRow TermsDocument { get; init; } = null!;

    /// <summary>Gets the version of the document the decision was last taken on.</summary>
    public int TermsDocumentVersion { get; init; }

    /// <summary>Gets a value indicating whether the document was accepted.</summary>
    public bool Accepted { get; init; }

    /// <summary>Gets when the decision was taken.</summary>
    public DateTimeOffset DecidedAt { get; init; }

    /// <summary>Gets the PNG image of the signature drawn to accept the document, if any.</summary>
    public byte[]? Signature { get; init; }

    /// <summary>Gets every change of the decision.</summary>
    public ICollection<TermsDecisionChangeRow> Changes { get; init; } = [];
}

/// <summary>
/// Stored change of a decision about a terms document of an event as the queries read it.
/// </summary>
public sealed class TermsDecisionChangeRow
{
    /// <summary>Gets the sequence of the change, which orders the changes made at once.</summary>
    public long Id { get; init; }

    /// <summary>Gets the identifier of the event.</summary>
    public Guid EventId { get; init; }

    /// <summary>Gets the identifier of the person the decision is about.</summary>
    public Guid UserId { get; init; }

    /// <summary>Gets the identifier of the terms document.</summary>
    public Guid TermsDocumentId { get; init; }

    /// <summary>Gets the version of the document the change was made on.</summary>
    public int TermsDocumentVersion { get; init; }

    /// <summary>Gets whether the document was accepted; false for a decline or a withdrawal.</summary>
    public bool Accepted { get; init; }

    /// <summary>Gets the identifier of the person who decided.</summary>
    public Guid DecidedById { get; init; }

    /// <summary>Gets the person who decided.</summary>
    public UserRow DecidedBy { get; init; } = null!;

    /// <summary>Gets the identifier of the account that recorded the change.</summary>
    public Guid RecordedById { get; init; }

    /// <summary>Gets when the change was made.</summary>
    public DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// Stored terms document as the queries read it.
/// </summary>
public sealed class TermsDocumentRow
{
    /// <summary>Gets the identifier of the document.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the rich-text content.</summary>
    public string Description { get; init; } = "{}";

    /// <summary>Gets a value indicating whether accepting the document asks for a signature.</summary>
    public bool RequiresSignature { get; init; }

    /// <summary>Gets whether the document holds conditions or a consent.</summary>
    public TermsDocumentKind Kind { get; init; }

    /// <summary>Gets the number of the current version.</summary>
    public int Version { get; init; }
}
