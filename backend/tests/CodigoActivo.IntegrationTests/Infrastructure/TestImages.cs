using NetVips;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public static class TestImages
{
    public const int Width = 40;
    public const int Height = 30;

    public static byte[] Png()
    {
        using var image = Solid();
        return image.PngsaveBuffer();
    }

    public static byte[] Gif()
    {
        using var image = Solid();
        return image.GifsaveBuffer();
    }

    public static (int Width, int Height, string Loader) Describe(byte[] encoded)
    {
        using var image = Image.NewFromBuffer(encoded);
        return (image.Width, image.Height, (string)image.Get("vips-loader"));
    }

    private static Image Solid()
    {
        using var black = Image.Black(Width, Height, bands: 3);
        using var colored = black + new[] { 200.0, 30, 60 };
        return colored.Cast(Enums.BandFormat.Uchar);
    }
}
