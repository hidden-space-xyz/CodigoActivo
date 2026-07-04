using System.Text;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Storage;
using FluentAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Storage;

public sealed class LocalFileSystemRepositoryTests : IDisposable
{
    private readonly string rootPath =
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    private readonly LocalFileSystemRepository sut;

    public LocalFileSystemRepositoryTests()
    {
        sut = new LocalFileSystemRepository(new FileStorageOptions { RootPath = rootPath });
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
    }

    [Fact]
    public void Constructor_creates_the_configured_root_directory()
    {
        Directory.Exists(rootPath).Should().BeTrue();
    }

    [Fact]
    public void Constructor_falls_back_to_the_files_directory_when_root_is_blank()
    {
        var repo = new LocalFileSystemRepository(new FileStorageOptions { RootPath = "   " });

        repo.Should().NotBeNull();
        Directory.Exists(Path.GetFullPath("files")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_then_OpenReadAsync_round_trips_the_bytes()
    {
        var payload = Encoding.UTF8.GetBytes("hello storage");
        await sut.SaveAsync("greeting.txt", new MemoryStream(payload));

        await using var stream = await sut.OpenReadAsync("greeting.txt");
        stream.Should().NotBeNull();
        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer);
        buffer.ToArray().Should().Equal(payload);
    }

    [Fact]
    public async Task OpenReadAsync_returns_null_for_a_missing_file()
    {
        (await sut.OpenReadAsync("does-not-exist.bin")).Should().BeNull();
    }

    [Fact]
    public async Task Delete_removes_an_existing_file()
    {
        await sut.SaveAsync("temp.dat", new MemoryStream([1, 2, 3]));

        sut.Delete("temp.dat");

        (await sut.OpenReadAsync("temp.dat")).Should().BeNull();
    }

    [Fact]
    public void Delete_is_a_no_op_for_a_missing_file()
    {
        sut.Invoking(s => s.Delete("nothing-here.dat")).Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("../x")]
    public void Delete_rejects_blank_and_path_traversal_names(string name)
    {
        sut.Invoking(s => s.Delete(name))
            .Should()
            .Throw<ArgumentException>()
            .WithParameterName("storedName");
    }

    [Fact]
    public async Task SaveAsync_rejects_a_path_traversal_name()
    {
        await sut.Invoking(s => s.SaveAsync("../escape.txt", new MemoryStream([0])))
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithParameterName("storedName");
    }
}
