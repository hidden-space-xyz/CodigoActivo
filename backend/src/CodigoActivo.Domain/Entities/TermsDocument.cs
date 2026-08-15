using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class TermsDocument : IdentifiableEntity
{
    public required string Name { get; set; }

    public string Description { get; set; } = "{}";
}
