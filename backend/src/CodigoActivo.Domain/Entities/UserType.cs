using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserType : NamedEntity
{
    public string Color { get; set; } = null!;

    public ICollection<User> Users { get; set; } = [];
}
