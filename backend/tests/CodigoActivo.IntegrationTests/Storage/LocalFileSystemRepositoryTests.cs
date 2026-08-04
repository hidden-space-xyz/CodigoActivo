using System.Text;
using AwesomeAssertions;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Storage;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Storage;

public sealed class LocalFileSystemRepositoryTests : IDisposable
{
    private static readonly string FallbackRoot = Path.GetFullPath("files");

    private readonly bool fallbackExistedBeforeTest = Directory.Exists(FallbackRoot);

    private readonly string rootPath = Path.Combine(
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
    public void Constructor_ValidRootPath_CreatesRootDirectory()
    {
        Directory.Exists(rootPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_BlankRootPath_FallsBackToFilesUnderTheWorkingDirectory()
    {
        _ = new LocalFileSystemRepository(new FileStorageOptions { RootPath = "   " });

        Directory.Exists(FallbackRoot).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_SavedFile_OpenReadAsyncRoundTripsBytes()
    {
        var payload = Encoding.UTF8.GetBytes("hello storage");

        await sut.SaveAsync("greeting.txt", new MemoryStream(payload), Ct);

        await using var stream = await sut.OpenReadAsync("greeting.txt", Ct);
        Assert.NotNull(stream);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, Ct);
        buffer.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task SaveAsync_NameAlreadyOnDisk_OverwritesTheWholeFile()
    {
        await sut.SaveAsync("dup.bin", new MemoryStream([1, 2, 3, 4, 5, 6]), Ct);

        await sut.SaveAsync("dup.bin", new MemoryStream([9, 9]), Ct);

        await using var stream = await sut.OpenReadAsync("dup.bin", Ct);
        Assert.NotNull(stream);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, Ct);
        buffer.ToArray().Should().Equal([9, 9]);
    }

    [Fact]
    public async Task OpenReadAsync_MissingFile_ReturnsNull()
    {
        var stream = await sut.OpenReadAsync("does-not-exist.bin", Ct);

        stream.Should().BeNull();
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFile()
    {
        await sut.SaveAsync("temp.dat", new MemoryStream([1, 2, 3]), Ct);

        sut.Delete("temp.dat");

        var stream = await sut.OpenReadAsync("temp.dat", Ct);
        stream.Should().BeNull();
    }

    [Fact]
    public void Delete_MissingFile_IsNoOp()
    {
        sut.Invoking(s => s.Delete("nothing-here.dat")).Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("../x")]
    [InlineData("/etc/passwd")]
    public void Delete_BlankOrPathTraversalName_ThrowsArgumentException(string name)
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
    public async Task SaveAsync_BlankOrPathTraversalName_ThrowsArgumentException(string name)
    {
        await sut.Invoking(s => s.SaveAsync(name, new MemoryStream([0]), Ct))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("storedName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../escape.txt")]
    public async Task OpenReadAsync_BlankOrPathTraversalName_ThrowsArgumentException(string name)
    {
        await sut.Invoking(s => s.OpenReadAsync(name, Ct))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("storedName");
    }
}
