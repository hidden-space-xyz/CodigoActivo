namespace CodigoActivo.Domain.Users;

/// <summary>
/// Relationship of a person with the association, which decides the roles they may sign up for
/// and whether they sign up early.
/// </summary>
public enum UserType
{
    /// <summary>
    /// Member of the association.
    /// </summary>
    Member,

    /// <summary>
    /// Person or organization that sponsors the association.
    /// </summary>
    Sponsor,

    /// <summary>
    /// Person who takes part in events without an organizing role.
    /// </summary>
    Participant,
}
