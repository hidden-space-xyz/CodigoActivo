namespace CodigoActivo.Domain.TermsDocuments;

/// <summary>
/// Tells what accepting a terms document means, which decides who accepts it for each person and
/// whether an event may require it.
/// </summary>
public enum TermsDocumentKind
{
    /// <summary>
    /// Conditions of taking part, such as rules or a participation authorization. The account
    /// holder accepts them for each person they sign up, and an event may require them.
    /// </summary>
    Conditions = 1,

    /// <summary>
    /// A consent, such as the use of images. It is decided for each person, by the person from the
    /// age of 14 and by their guardian before, and an event never requires it, so declining it
    /// never stops a signup.
    /// </summary>
    Consent = 2,
}
