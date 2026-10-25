namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Narrows an extract to what the request needs (article 5.1.c GDPR): a range of days and the
/// kinds of data, named after the entities of the history.
/// </summary>
/// <param name="From">First day covered, or <see langword="null"/> for no limit.</param>
/// <param name="To">Last day covered, or <see langword="null"/> for no limit.</param>
/// <param name="Entities">Kinds of data covered; empty for every kind.</param>
public sealed record BlockedDataFilter(
    DateOnly? From,
    DateOnly? To,
    IReadOnlyCollection<string> Entities
);
