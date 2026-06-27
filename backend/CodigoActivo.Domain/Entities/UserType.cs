using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class UserType : NamedEntity
{
    public ICollection<UserTypeAssignment> Assignments { get; set; } = [];
}
