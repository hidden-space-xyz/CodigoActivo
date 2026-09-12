using System.Text;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Storage;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Storage;

public sealed class LocalFileSystemRepositoryTests : IDisposable
{
    private static readonly string FallbackRoot = Path.GetFullPath("files");

    private readonly bool fallbackExistedBeforeTest = Directory.Exists(FallbackRoot);

    private readonly string rootPath = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-storage-tests",
        Guid.NewGuid().ToString("N")
    );

    private readonly LocalFileSystemRepository sut;

    public LocalFileSystemRepositoryTests()
    {
        sut = new LocalFileSystemRepository(new FileStorageOptions { RootPath = rootPath });
    }

    public void Dispose()
    {
        DeleteDirectory(rootPath);

        if (!fallbackExistedBeforeTest)
        {
            DeleteDirectory(FallbackRoot);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void ConstructorValidRootPathCreatesRootDirectory()
    {
        Directory.Exists(rootPath).Should().BeTrue();
    }

    [Fact]
    public void ConstructorBlankRootPathFallsBackToFilesUnderTheWorkingDirectory()
    {
        _ = new LocalFileSystemRepository(new FileStorageOptions { RootPath = "   " });

        Directory.Exists(FallbackRoot).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsyncSavedFileOpenReadAsyncRoundTripsBytes()
    {
        var payload = Encoding.UTF8.GetBytes("hello storage");
        await using var payloadStream = new MemoryStream(payload);

        await sut.SaveAsync("greeting.txt", payloadStream, Ct);

        await using var stream = await sut.OpenReadAsync("greeting.txt", Ct);
        Assert.NotNull(stream);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, Ct);
        buffer.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task SaveAsyncNameAlreadyOnDiskOverwritesTheWholeFile()
    {
        await using (var original = new MemoryStream([1, 2, 3, 4, 5, 6]))
        {
            await sut.SaveAsync("dup.bin", original, Ct);
        }

        await using (var replacement = new MemoryStream([9, 9]))
        {
            await sut.SaveAsync("dup.bin", replacement, Ct);
        }

        await using var stream = await sut.OpenReadAsync("dup.bin", Ct);
        Assert.NotNull(stream);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, Ct);
        buffer.ToArray().Should().Equal([9, 9]);
    }

    [Fact]
    public async Task OpenReadAsyncMissingFileReturnsNull()
    {
        var stream = await sut.OpenReadAsync("does-not-exist.bin", Ct);

        stream.Should().BeNull();
    }

    [Fact]
    public async Task DeleteExistingFileRemovesFile()
    {
        await using var content = new MemoryStream([1, 2, 3]);
        await sut.SaveAsync("temp.dat", content, Ct);

        sut.Delete("temp.dat");

        var stream = await sut.OpenReadAsync("temp.dat", Ct);
        stream.Should().BeNull();
    }

    [Fact]
    public void DeleteMissingFileIsNoOp()
    {
        sut.Invoking(s => s.Delete("nothing-here.dat")).Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("../x")]
    [InlineData("/etc/passwd")]
    public void DeleteBlankOrPathTraversalNameThrowsArgumentException(string name)
    {
        sut.Invoking(s => s.Delete(name))
            .Should()
            .Throw<ArgumentException>()
            .WithParameterName("storedName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../escape.txt")]
    [InlineData("nested/escape.txt")]
    public async Task SaveAsyncBlankOrPathTraversalNameThrowsArgumentException(string name)
    {
        await using var content = new MemoryStream([0]);
        await sut.Invoking(s => s.SaveAsync(name, content, Ct))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("storedName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../escape.txt")]
    public async Task OpenReadAsyncBlankOrPathTraversalNameThrowsArgumentException(string name)
    {
        await sut.Invoking(s => s.OpenReadAsync(name, Ct))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("storedName");
    }
}
