using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserType : NamedEntity
{
    public string Color { get; set; } = null!;

    public bool Hidden { get; set; }

    public bool IsAllowedForMinors { get; set; }

    public bool IsAllowedForAdults { get; set; }

    public ICollection<User> Users { get; set; } = [];
}