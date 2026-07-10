using System.Text;
using AwesomeAssertions;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Storage;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Storage;

public sealed class LocalFileSystemRepositoryTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private readonly LocalFileSystemRepository sut;

    public LocalFileSystemRepositoryTests()
    {
        sut = new LocalFileSystemRepository(new FileStorageOptions { RootPath = rootPath });
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
    }

    [Fact]
    public void Constructor_ValidRootPath_CreatesRootDirectory()
    {
        Directory.Exists(rootPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_BlankRootPath_FallsBackToFilesDirectory()
    {
        var repo = new LocalFileSystemRepository(new FileStorageOptions { RootPath = "   " });

        repo.Should().NotBeNull();
        Directory.Exists(Path.GetFullPath("files")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_SavedFile_OpenReadAsyncRoundTripsBytes()
    {
        var payload = Encoding.UTF8.GetBytes("hello storage");
        await sut.SaveAsync(
            "greeting.txt",
            new MemoryStream(payload),
            TestContext.Current.CancellationToken
        );

        await using var stream = await sut.OpenReadAsync(
            "greeting.txt",
            TestContext.Current.CancellationToken
        );
        stream.Should().NotBeNull();
        await using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        buffer.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task OpenReadAsync_MissingFile_ReturnsNull()
    {
        (await sut.OpenReadAsync("does-not-exist.bin", TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
    }

    [Fact]
    public async Task Delete_ExistingFile_RemovesFile()
    {
        await sut.SaveAsync(
            "temp.dat",
            new MemoryStream([1, 2, 3]),
            TestContext.Current.CancellationToken
        );

        sut.Delete("temp.dat");

        (await sut.OpenReadAsync("temp.dat", TestContext.Current.CancellationToken))
            .Should()
            .BeNull();
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
    public void Delete_BlankOrPathTraversalName_ThrowsArgumentException(string name)
    {
        sut.Invoking(s => s.Delete(name))
            .Should()
            .Throw<ArgumentException>()
            .WithParameterName("storedName");
    }

    [Fact]
    public async Task SaveAsync_PathTraversalName_ThrowsArgumentException()
    {
        await sut.Invoking(s =>
                s.SaveAsync(
                    "../escape.txt",
                    new MemoryStream([0]),
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("storedName");
    }
}
