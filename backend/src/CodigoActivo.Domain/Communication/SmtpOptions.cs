namespace CodigoActivo.Domain.Communication;

public enum SmtpSecurityMode
{
    StartTls = 0,
    SslOnConnect = 1,
    None = 2,
    Auto = 3,
}

public sealed class SmtpOptions
{
    public const int DefaultPort = 587;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = DefaultPort;

    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;
}
