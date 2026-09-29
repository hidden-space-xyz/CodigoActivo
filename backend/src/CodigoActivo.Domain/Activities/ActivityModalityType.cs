using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// How an activity is attended, from the fixed catalog of modalities.
/// </summary>
public class ActivityModalityType : IdentifiableEntity
{
    private ActivityModalityType() { }

    /// <summary>
    /// Gets the human-readable name.
    /// </summary>
    public string Name { get; private init; } = string.Empty;

    /// <summary>
    /// Creates an entry of the catalog.
    /// </summary>
    /// <param name="id">Known identifier of the modality.</param>
    /// <param name="name">Human-readable name.</param>
    /// <returns>The unsaved entry.</returns>
    public static ActivityModalityType Create(Guid id, string name)
    {
        return new ActivityModalityType { Id = id, Name = name };
    }
}
