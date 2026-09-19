using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Represents an email accent value used by the application.
/// </summary>
/// <param name="Line">The line value.</param>
/// <param name="Soft">The soft value.</param>
/// <param name="Ink">The ink value.</param>
public sealed record EmailAccent(string Line, string Soft, string Ink);

/// <summary>
/// Provides brand assets and presentation values for transactional emails.
/// </summary>
public static class EmailBranding
{
    /// <summary>
    /// Identifies the font stack configuration or policy value.
    /// </summary>
    public const string FontStack =
        "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif";

    /// <summary>
    /// Identifies the canvas configuration or policy value.
    /// </summary>
    public const string Canvas = "#f5f5f5";

    /// <summary>
    /// Identifies the surface configuration or policy value.
    /// </summary>
    public const string Surface = "#ffffff";

    /// <summary>
    /// Identifies the panel configuration or policy value.
    /// </summary>
    public const string Panel = "#f5f5f5";

    /// <summary>
    /// Identifies the border configuration or policy value.
    /// </summary>
    public const string Border = "#e8e8e8";

    /// <summary>
    /// Identifies the border soft configuration or policy value.
    /// </summary>
    public const string BorderSoft = "#efefef";

    /// <summary>
    /// Identifies the text bright configuration or policy value.
    /// </summary>
    public const string TextBright = "#171717";

    /// <summary>
    /// Identifies the text configuration or policy value.
    /// </summary>
    public const string Text = "#262626";

    /// <summary>
    /// Identifies the text muted configuration or policy value.
    /// </summary>
    public const string TextMuted = "#616161";

    /// <summary>
    /// Identifies the text dim configuration or policy value.
    /// </summary>
    public const string TextDim = "#737373";

    /// <summary>
    /// Identifies the primary configuration or policy value.
    /// </summary>
    public const string Primary = "#f9a320";

    /// <summary>
    /// Identifies the primary ink configuration or policy value.
    /// </summary>
    public const string PrimaryInk = "#8f5900";

    /// <summary>
    /// Identifies the primary soft configuration or policy value.
    /// </summary>
    public const string PrimarySoft = "#fef3e2";

    /// <summary>
    /// Identifies the on primary configuration or policy value.
    /// </summary>
    public const string OnPrimary = "#2a1400";

    /// <summary>
    /// Identifies the logo content identifier configuration or policy value.
    /// </summary>
    public const string LogoContentId = "codigoactivo-logo";

    /// <summary>
    /// Identifies the logo file name configuration or policy value.
    /// </summary>
    public const string LogoFileName = "codigo-activo.png";

    /// <summary>
    /// Stores the shared brand value.
    /// </summary>
    public static readonly EmailAccent Brand = new(Primary, PrimarySoft, PrimaryInk);

    /// <summary>
    /// Stores the shared success value.
    /// </summary>
    public static readonly EmailAccent Success = new("#2e9e57", "#e4f2e9", "#257f46");

    /// <summary>
    /// Stores the shared danger value.
    /// </summary>
    public static readonly EmailAccent Danger = new("#d84a3b", "#fae8e6", "#c13b2e");

    /// <summary>
    /// Stores the shared inline images value.
    /// </summary>
    public static readonly IReadOnlyList<EmailInlineImage> InlineImages =
    [
        new EmailInlineImage(LogoContentId, LogoFileName, "image/png", ReadLogo()),
    ];

    private const string LogoResourceName =
        "CodigoActivo.Application.Resources.Images.logo-mark.png";

    private static byte[] ReadLogo()
    {
        using var stream =
            typeof(EmailBranding).Assembly.GetManifestResourceStream(LogoResourceName)
            ?? throw new InvalidOperationException(
                $"Missing embedded resource '{LogoResourceName}'."
            );

        var buffer = new byte[stream.Length];
        stream.ReadExactly(buffer);
        return buffer;
    }
}
