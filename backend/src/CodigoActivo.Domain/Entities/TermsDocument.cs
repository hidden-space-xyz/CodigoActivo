using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Parses and validates terms documents.
/// </summary>
public class TermsDocument : IdentifiableEntity
{
    /// <summary>
    /// Gets or sets the human-readable name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the detailed description.
    /// </summary>
    public string Description { get; set; } = "{}";
}
