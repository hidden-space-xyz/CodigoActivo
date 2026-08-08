using System.Linq.Expressions;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.Files;

internal static class FileTestData
{
    public static byte[] PngBytes()
    {
        var bytes = new byte[32];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        bytes[8] = 0x00;
        bytes[9] = 0x00;
        bytes[10] = 0x00;
        bytes[11] = 0x0D;
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        bytes[19] = 0x01;
        bytes[23] = 0x01;
        return bytes;
    }

    public static MemoryStream PngStream()
    {
        return new(PngBytes(), writable: false);
    }

    public static MemoryStream JunkStream()
    {
        return new(new byte[32], writable: false);
    }

    public static FileEntity NewFile(string name = "photo.png", string extension = "png")
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Extension = extension,
            UploadedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UploadedBy = Guid.NewGuid(),
        };
    }

    public static void FileFound(this IFileRepository files, FileEntity file)
    {
        files.Finds(file);
        files
            .GetAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([file]);
    }

    public static void FileMissing(this IFileRepository files)
    {
        files.Finds(null);
        files
            .GetAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    public static void FileReferenced(this IFileRepository files, bool referenced)
    {
        files.IsInUseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(referenced);
    }
}
