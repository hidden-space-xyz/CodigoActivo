using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserStatusType : NamedEntity
{
    public required string Color { get; set; }

    public ICollection<User> Users { get; set; } = [];
}
