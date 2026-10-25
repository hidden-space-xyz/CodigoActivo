namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Designates the person responsible for data protection, the only one who may consult the
/// blocked data: the administrator whose verified email is this one. It is a deployment setting
/// (<c>PRIVACY_OFFICER_EMAIL</c>), so no administrator can grant it to themselves.
/// </summary>
public sealed class PrivacyOfficerOptions
{
    /// <summary>
    /// Gets or sets the normalized email of the designated account.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}
