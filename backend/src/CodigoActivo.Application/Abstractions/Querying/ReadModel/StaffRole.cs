namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Staff role of an account, as clients send and receive it; each role includes the rights of the
/// roles before it.
/// </summary>
public enum StaffRole
{
    /// <summary>
    /// No staff role.
    /// </summary>
    None = 0,

    /// <summary>
    /// Publishes content.
    /// </summary>
    Editor = 1,

    /// <summary>
    /// Manages people on top of what an editor does.
    /// </summary>
    Administrator = 2,
}
