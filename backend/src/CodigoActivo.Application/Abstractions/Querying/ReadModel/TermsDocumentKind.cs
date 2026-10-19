namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Whether a terms document holds conditions or a consent, as clients send and receive it.
/// </summary>
public enum TermsDocumentKind
{
    /// <summary>
    /// Conditions of taking part, accepted by the account holder and possibly required.
    /// </summary>
    Conditions = 1,

    /// <summary>
    /// A consent, decided per person and never required.
    /// </summary>
    Consent = 2,
}
