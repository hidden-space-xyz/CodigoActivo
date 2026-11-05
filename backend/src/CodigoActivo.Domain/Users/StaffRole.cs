namespace CodigoActivo.Domain.Users;

/// <summary>
/// Staff role of an account in the association, each including the rights of the roles before it.
/// The initial administrator, the superadministrator, is always an administrator.
/// </summary>
public enum StaffRole
{
    /// <summary>
    /// No staff role: the account manages only itself and its dependents.
    /// </summary>
    None = 0,

    /// <summary>
    /// Publishes content: events and activities with their catalogs and terms documents, news,
    /// partners, resources and files.
    /// </summary>
    Editor = 1,

    /// <summary>
    /// Manages people on top of what an editor does: users, signups, reports, every manual email,
    /// the diagnostic log and the legal policies.
    /// </summary>
    Administrator = 2,
}
