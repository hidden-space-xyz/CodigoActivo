namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Identifies the supported smtp security mode values.
/// </summary>
public enum SmtpSecurityMode
{
    /// <summary>
    /// Selects the start tls option.
    /// </summary>
    StartTls = 0,

    /// <summary>
    /// Selects the ssl on connect option.
    /// </summary>
    SslOnConnect = 1,

    /// <summary>
    /// No option is selected.
    /// </summary>
    None = 2,

    /// <summary>
    /// Selects the auto option.
    /// </summary>
    Auto = 3,
}

/// <summary>
/// Defines configuration values for smtp.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>
    /// Identifies the default port configuration or policy value.
    /// </summary>
    public const int DefaultPort = 587;

    /// <summary>
    /// Gets or sets the host value.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port value.
    /// </summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// Gets or sets the security value.
    /// </summary>
    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

    /// <summary>
    /// Gets or sets the username value.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password value.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the from address value.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the from name value.
    /// </summary>
    public string FromName { get; set; } = string.Empty;
}
