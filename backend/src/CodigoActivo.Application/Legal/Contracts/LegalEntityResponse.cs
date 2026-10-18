namespace CodigoActivo.Application.Legal.Contracts;

/// <summary>
/// Contains the legal identity of the association, as the privacy policy shows it.
/// </summary>
/// <param name="Name">Registered name of the association.</param>
/// <param name="TaxId">Tax identification number (NIF).</param>
/// <param name="Address">Registered postal address.</param>
/// <param name="Registry">Register of associations and the entry number in it.</param>
public sealed record LegalEntityResponse(
    string Name,
    string TaxId,
    string Address,
    string Registry
);
