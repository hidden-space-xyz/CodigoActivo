using System.Text.Json.Serialization;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Identifies the supported gender values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Gender>))]
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
