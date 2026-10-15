using System.Buffers.Binary;

namespace CodigoActivo.UnitTests.TestSupport;

public static class SignatureImages
{
    public static byte[] Png(uint width = 600, uint height = 200, int length = 33)
    {
        var png = new byte[length];
        Convert.FromHexString("89504E470D0A1A0A0000000D49484452").CopyTo(png, 0);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20, 4), height);
        return png;
    }
}
