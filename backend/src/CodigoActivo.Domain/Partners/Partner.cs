using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Partners;

/// <summary>
/// Organisation that supports the association, listed by sponsorship tier. Its profile changes
/// only as a whole, recording who made the change.
/// </summary>
public class Partner : AuditableEntity, IAggregateRoot
{
    private Partner() { }

    /// <summary>
    /// Gets the human-readable name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the date the partnership started.
    /// </summary>
    public DateOnly FromDate { get; private set; }

    /// <summary>
    /// Gets the sponsorship tier.
    /// </summary>
    public int Tier { get; private set; }

    /// <summary>
    /// Gets the website, if any.
    /// </summary>
    public string? Web { get; private set; }

    /// <summary>
    /// Gets the identifier of the logo file.
    /// </summary>
    public Guid ThumbnailId { get; private set; }

    /// <summary>
    /// Creates a partner.
    /// </summary>
    /// <param name="details">Profile of the partner.</param>
    /// <param name="authorId">Identifier of the user who creates it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new partner.</returns>
    public static Partner Create(PartnerDetails details, Guid authorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        var partner = new Partner();
        partner.Apply(details);
        partner.RecordCreation(authorId, now);
        return partner;
    }

    /// <summary>
    /// Replaces the profile of the partner.
    /// </summary>
    /// <param name="details">New profile.</param>
    /// <param name="editorId">Identifier of the user who edits it.</param>
    /// <param name="now">Current time.</param>
    public void Update(PartnerDetails details, Guid editorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        Apply(details);
        RecordUpdate(editorId, now);
    }

    private void Apply(PartnerDetails details)
    {
        Name = details.Name.Trim();
        FromDate = details.FromDate;
        Tier = details.Tier;
        Web = details.Web.NormalizeOrNull();
        ThumbnailId = details.ThumbnailId;
    }
}
