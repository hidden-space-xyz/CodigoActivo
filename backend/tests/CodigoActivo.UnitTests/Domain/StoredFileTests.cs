using AwesomeAssertions;
using CodigoActivo.Domain.Files;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class StoredFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void UploadRecordsNameFormatUploaderAndTime()
    {
        var uploaderId = Guid.NewGuid();

        var file = StoredFile.Upload("foto.png", "png", uploaderId, Now);

        file.Id.Should().NotBeEmpty();
        file.Name.Should().Be("foto.png");
        file.Extension.Should().Be("png");
        file.UploadedBy.Should().Be(uploaderId);
        file.UploadedAt.Should().Be(Now);
    }

    [Fact]
    public void ReplaceKeepsIdentityAndUploader()
    {
        var uploaderId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var file = StoredFile.Upload("foto.png", "png", uploaderId, Now, id);

        file.Replace("nueva.jpg", "jpg", Now.AddHours(1));

        file.Id.Should().Be(id);
        file.UploadedBy.Should().Be(uploaderId);
        file.Name.Should().Be("nueva.jpg");
        file.Extension.Should().Be("jpg");
        file.UploadedAt.Should().Be(Now.AddHours(1));
    }
}
