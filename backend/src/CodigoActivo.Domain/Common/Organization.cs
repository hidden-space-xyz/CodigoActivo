namespace CodigoActivo.Domain.Common;

/// <summary>
/// Public identity of the association that runs the application. Its legal identity and contact
/// details are deployment settings instead, so they stay out of the public repository.
/// </summary>
public static class Organization
{
    /// <summary>
    /// Name the association uses in the application, its email and authenticator apps.
    /// </summary>
    public const string Name = "Código Activo";

    /// <summary>
    /// Internet domain of the association's website and mailboxes.
    /// </summary>
    public const string WebDomain = "codigoactivo.es";
}
