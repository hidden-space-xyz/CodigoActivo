namespace CodigoActivo.Domain.Common;

/// <summary>
/// Represents a dashboard counts value used by the application.
/// </summary>
public sealed record DashboardCounts
{
    /// <summary>
    /// Gets or sets the events value.
    /// </summary>
    public int Events { get; init; }
    /// <summary>
    /// Gets or sets the activities value.
    /// </summary>
    public int Activities { get; init; }
    /// <summary>
    /// Gets or sets the resources value.
    /// </summary>
    public int Resources { get; init; }
    /// <summary>
    /// Gets or sets the announcements value.
    /// </summary>
    public int Announcements { get; init; }
    /// <summary>
    /// Gets or sets the partners value.
    /// </summary>
    public int Partners { get; init; }
    /// <summary>
    /// Gets or sets the users value.
    /// </summary>
    public int Users { get; init; }
}
