using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserType : NamedEntity
{
    public required string Color { get; set; }

    public ICollection<User> Users { get; set; } = [];
}
