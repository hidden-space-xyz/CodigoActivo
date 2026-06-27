using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserStatusType : NamedEntity
{
    public ICollection<User> Users { get; set; } = [];
}
