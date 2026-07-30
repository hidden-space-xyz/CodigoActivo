using System.Text.Json.Serialization;

namespace CodigoActivo.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter<Gender>))]
public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3,
}
