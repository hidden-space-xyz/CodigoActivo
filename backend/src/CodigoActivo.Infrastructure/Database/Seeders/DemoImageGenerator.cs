using NetVips;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Draws the demo images locally: a diagonal gradient between two colors chosen by the file
/// identifier, so the same file always gets the same image and seeding needs no network.
/// </summary>
internal static class DemoImageGenerator
{
    /// <summary>
    /// Width of every demo image.
    /// </summary>
    internal const int Width = 1080;

    /// <summary>
    /// Height of every demo image.
    /// </summary>
    internal const int Height = 720;

    private const int JpegQuality = 90;

    private static readonly (double[] From, double[] To)[] Gradients =
    [
        ([20, 108, 148], [12, 44, 84]),
        ([236, 120, 64], [148, 36, 72]),
        ([72, 160, 96], [20, 84, 92]),
        ([116, 92, 196], [40, 40, 112]),
        ([236, 184, 72], [196, 84, 48]),
        ([84, 148, 212], [124, 64, 156]),
    ];

    /// <summary>
    /// Encodes as JPEG the gradient of a demo file.
    /// </summary>
    /// <param name="fileId">Identifier of the demo file.</param>
    /// <returns>The encoded image.</returns>
    internal static byte[] Generate(Guid fileId)
    {
        var (from, to) = Gradients[fileId.ToByteArray()[0] % Gradients.Length];
        var steps = Width + Height - 2.0;
        var slope = from.Zip(to, (start, end) => (end - start) / steps).ToArray();

        using var coordinates = Image.Xyz(Width, Height);
        using var x = coordinates[0];
        using var y = coordinates[1];
        using var distance = x + y;
        using var gradient = distance.Linear(slope, from);
        using var pixels = gradient.Cast(Enums.BandFormat.Uchar);
        using var srgb = pixels.Copy(interpretation: Enums.Interpretation.Srgb);
        return srgb.JpegsaveBuffer(q: JpegQuality);
    }
}
