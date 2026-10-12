namespace CodigoActivo.Composition.Accounts;

/// <summary>
/// Settings of the initial administrator, used only while the user table is empty.
/// </summary>
internal sealed class InitialAdministratorOptions
{
    /// <summary>
    /// Gets or sets the email of the initial administrator, empty when none is configured.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}
