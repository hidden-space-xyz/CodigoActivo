using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserStatusType : NamedEntity
{
    public string Color { get; set; } = null!;

    public ICollection<User> Users { get; set; } = [];
}
