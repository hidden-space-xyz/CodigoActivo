using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Colors of an accent that sets the tone of an email or of a callout.
/// </summary>
/// <param name="Line">Strong color of the bar at the top of the card and of the left edge of a callout.</param>
/// <param name="Soft">Light tint of the accent color.</param>
/// <param name="Ink">Dark shade of the accent color.</param>
public sealed record EmailAccent(string Line, string Soft, string Ink);

/// <summary>
/// Provides the brand assets and colors of the emails.
/// </summary>
public static class EmailBranding
{
    /// <summary>
    /// System font stack of the email texts.
    /// </summary>
    public const string FontStack =
        "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif";

    /// <summary>
    /// Background color around the card and of its footer.
    /// </summary>
    public const string Canvas = "#f5f5f5";

    /// <summary>
    /// Background color of the card.
    /// </summary>
    public const string Surface = "#ffffff";

    /// <summary>
    /// Background color of panels, callouts and the one-time code box.
    /// </summary>
    public const string Panel = "#f5f5f5";

    /// <summary>
    /// Color of the borders of the card, panels, callouts and code box, and of the lines between detail rows.
    /// </summary>
    public const string Border = "#e8e8e8";

    /// <summary>
    /// Color of the lines under the header and above the footer.
    /// </summary>
    public const string BorderSoft = "#efefef";

    /// <summary>
    /// Color of the brand names, the heading and the one-time code.
    /// </summary>
    public const string TextBright = "#171717";

    /// <summary>
    /// Color of paragraphs, callouts and detail values.
    /// </summary>
    public const string Text = "#262626";

    /// <summary>
    /// Color of notes, footer texts and detail labels.
    /// </summary>
    public const string TextMuted = "#616161";

    /// <summary>
    /// Brand orange of the buttons and of the brand accent.
    /// </summary>
    public const string Primary = "#f9a320";

    /// <summary>
    /// Dark brand orange of the links, legible on the light backgrounds.
    /// </summary>
    public const string PrimaryInk = "#8f5900";

    /// <summary>
    /// Light tint of the brand orange.
    /// </summary>
    public const string PrimarySoft = "#fef3e2";

    /// <summary>
    /// Color of the button labels on the brand orange.
    /// </summary>
    public const string OnPrimary = "#2a1400";

    /// <summary>
    /// Content identifier by which the HTML part references the inline logo.
    /// </summary>
    public const string LogoContentId = "codigoactivo-logo";

    /// <summary>
    /// File name of the inline logo image.
    /// </summary>
    public const string LogoFileName = "codigo-activo.png";

    /// <summary>
    /// Brand orange accent, used by most emails.
    /// </summary>
    public static readonly EmailAccent Brand = new(Primary, PrimarySoft, PrimaryInk);

    /// <summary>
    /// Green accent for good news, such as a confirmed signup.
    /// </summary>
    public static readonly EmailAccent Success = new("#2e9e57", "#e4f2e9", "#257f46");

    /// <summary>
    /// Red accent for bad news or a required action, such as a denied signup or a withdrawn consent.
    /// </summary>
    public static readonly EmailAccent Danger = new("#d84a3b", "#fae8e6", "#c13b2e");

    /// <summary>
    /// Inline images every rendered email carries: the logo, read once from the embedded resource.
    /// </summary>
    public static readonly IReadOnlyList<EmailInlineImage> InlineImages =
    [
        new EmailInlineImage(LogoContentId, LogoFileName, "image/png", ReadLogo()),
    ];

    private const string LogoResourceName =
        "CodigoActivo.Infrastructure.Communication.Templates.Images.logo-mark.png";

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
