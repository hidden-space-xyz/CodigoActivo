namespace CodigoActivo.Application.Users.Contracts;

/// <summary>
/// Gender a person gives, as clients send and receive it.
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
    /// The value does not match the predefined options.
    /// </summary>
    Other = 3,

    /// <summary>
    /// The user prefers not to disclose their gender.
    /// </summary>
    PreferNotToSay = 4,
}
