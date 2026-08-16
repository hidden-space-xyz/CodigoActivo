using CodigoActivo.Domain.Communication;

namespace CodigoActivo.Application.Emails;

public sealed record EmailAccent(string Line, string Soft, string Ink);

public static class EmailBranding
{
    public const string FontStack =
        "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif";

    public const string Canvas = "#f5f5f5";
    public const string Surface = "#ffffff";
    public const string Panel = "#f5f5f5";
    public const string Border = "#e8e8e8";
    public const string BorderSoft = "#efefef";

    public const string TextBright = "#171717";
    public const string Text = "#262626";
    public const string TextMuted = "#616161";
    public const string TextDim = "#737373";

    public const string Primary = "#f9a320";
    public const string PrimaryInk = "#8f5900";
    public const string PrimarySoft = "#fef3e2";
    public const string OnPrimary = "#2a1400";

    public const string LogoContentId = "codigoactivo-logo";
    public const string LogoFileName = "codigo-activo.png";

    public static readonly EmailAccent Brand = new(Primary, PrimarySoft, PrimaryInk);
    public static readonly EmailAccent Success = new("#2e9e57", "#e4f2e9", "#257f46");
    public static readonly EmailAccent Danger = new("#d84a3b", "#fae8e6", "#c13b2e");

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
