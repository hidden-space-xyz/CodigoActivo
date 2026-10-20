using AwesomeAssertions;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Files;

public sealed class StoredFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void UploadRecordsNameFormatUploaderAndTime()
    {
        var uploaderId = Guid.NewGuid();

        var file = StoredFile.Upload("foto.png", "png", UserId.From(uploaderId), Now);

        file.Id.Value.Should().NotBeEmpty();
        file.Name.Should().Be("foto.png");
        file.Extension.Should().Be("png");
        file.UploadedBy.Value.Should().Be(uploaderId);
        file.UploadedAt.Should().Be(Now);
    }
}
