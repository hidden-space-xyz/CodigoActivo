namespace CodigoActivo.Application.Legal.Contracts;

/// <summary>
/// Contains the legal identity and contact details of the association, as the site shows them.
/// </summary>
/// <param name="Name">Registered name of the association.</param>
/// <param name="TaxId">Tax identification number (NIF).</param>
/// <param name="Address">Registered postal address.</param>
/// <param name="Registry">Register of associations and the entry number in it.</param>
/// <param name="Email">Contact mailbox.</param>
/// <param name="Phone">Contact phone, or <see langword="null"/> when none is published.</param>
public sealed record LegalEntityResponse(
    string Name,
    string TaxId,
    string Address,
    string Registry,
    string Email,
    string? Phone
);
