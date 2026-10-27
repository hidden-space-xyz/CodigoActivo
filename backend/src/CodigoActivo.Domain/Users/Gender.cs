namespace CodigoActivo.Domain.Users;

/// <summary>
/// Gender a person declares in their profile.
/// </summary>
public enum Gender
{
    /// <summary>
    /// The user identifies as male.
    /// </summary>
    Male = 1,

    /// <summary>
    /// The user identifies as female.
    /// </summary>
    Female = 2,

    /// <summary>
    /// The user identifies with none of the other options.
    /// </summary>
    Other = 3,

    /// <summary>
    /// The user prefers not to disclose their gender.
    /// </summary>
    PreferNotToSay = 4,
}
