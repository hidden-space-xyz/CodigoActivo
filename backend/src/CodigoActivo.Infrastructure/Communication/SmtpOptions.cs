namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Identifies how the connection to the SMTP server is encrypted, read by name, ignoring case, from
/// <c>SMTP_SECURITY</c>.
/// </summary>
public enum SmtpSecurityMode
{
    /// <summary>
    /// Upgrades the connection with STARTTLS and fails when the server does not offer it.
    /// </summary>
    StartTls = 0,

    /// <summary>
    /// Speaks TLS from the first byte, as servers on <see cref="SmtpOptions.ImplicitTlsPort"/> expect.
    /// </summary>
    SslOnConnect = 1,

    /// <summary>
    /// Sends everything unencrypted, for a local mail catcher; Production refuses to start with it.
    /// </summary>
    None = 2,

    /// <summary>
    /// Lets MailKit choose from the port and upgrade only when the server offers STARTTLS, so the
    /// connection may stay unencrypted; Production refuses to start with it.
    /// </summary>
    Auto = 3,
}

/// <summary>
/// Defines the SMTP server that delivers every email, read from the <c>SMTP_*</c> deployment
/// settings; the start fails without <c>SMTP_HOST</c> and <c>SMTP_FROM_ADDRESS</c>.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>
    /// Identifies the port used when <c>SMTP_PORT</c> is not set, the submission port.
    /// </summary>
    public const int DefaultPort = 587;

    /// <summary>
    /// Identifies the port whose servers expect TLS from the first byte.
    /// </summary>
    public const int ImplicitTlsPort = 465;

    /// <summary>
    /// Gets or sets the name or address of the SMTP server (<c>SMTP_HOST</c>); the sender refuses to
    /// send while it is empty.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port of the SMTP server (<c>SMTP_PORT</c>), between 1 and 65535.
    /// </summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// Gets or sets how the connection is encrypted (<c>SMTP_SECURITY</c>); when the setting is
    /// empty, <see cref="DefaultSecurity"/> derives it from <see cref="Port"/>.
    /// </summary>
    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

    /// <summary>
    /// Gets or sets the account the sender authenticates with (<c>SMTP_USERNAME</c>), taken
    /// verbatim; when it is empty the sender does not authenticate.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password of <see cref="Username"/> (the <c>SMTP_PASSWORD</c> secret), taken
    /// verbatim and used only when a username is set.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender address of every email (<c>SMTP_FROM_ADDRESS</c>); the sender refuses
    /// to send while it is empty.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender display name (<c>SMTP_FROM_NAME</c>), the name of the association
    /// when the setting is empty.
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// Chooses the encryption a port implies when none is configured: TLS from the first byte on
    /// <see cref="ImplicitTlsPort"/> and a mandatory STARTTLS upgrade on any other port.
    /// </summary>
    /// <param name="port">Port of the SMTP server.</param>
    /// <returns>The security mode the port implies.</returns>
    public static SmtpSecurityMode DefaultSecurity(int port)
    {
        return port == ImplicitTlsPort ? SmtpSecurityMode.SslOnConnect : SmtpSecurityMode.StartTls;
    }
}
