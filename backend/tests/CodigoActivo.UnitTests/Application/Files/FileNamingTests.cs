using AwesomeAssertions;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Files;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Files;

public sealed class FileNamingTests
{
    private static readonly StoredFileId Id = StoredFileId.From(
        Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")
    );

    [Fact]
    public void StoredNameFullVariantIsTheIdAndExtension()
    {
        FileNaming
            .StoredName(Id, "webp", FileVariant.Full)
            .Should()
            .Be("0f8fad5b-d9cb-469f-a165-70867728950e.webp");
    }

    [Fact]
    public void StoredNameThumbnailVariantMarksTheName()
    {
        FileNaming
            .StoredName(Id, "webp", FileVariant.Thumbnail)
            .Should()
            .Be("0f8fad5b-d9cb-469f-a165-70867728950e.thumbnail.webp");
    }

    [Fact]
    public void StoredNamesListsEveryVariant()
    {
        FileNaming
            .StoredNames(Id, "webp")
            .Should()
            .Equal(
                "0f8fad5b-d9cb-469f-a165-70867728950e.webp",
                "0f8fad5b-d9cb-469f-a165-70867728950e.thumbnail.webp"
            );
    }

    [Theory]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e.webp")]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e.thumbnail.webp")]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e.jpg")]
    public void TryReadIdNameOfAFileReturnsItsId(string storedName)
    {
        FileNaming.TryReadId(storedName, out var id).Should().BeTrue();
        id.Should().Be(Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData(".0f8fad5bd9cb469fa16570867728950e.tmp")]
    [InlineData("0f8fad5bd9cb469fa16570867728950e.webp")]
    [InlineData("notes.txt")]
    public void TryReadIdNameWithoutALeadingFileIdReturnsFalse(string storedName)
    {
        FileNaming.TryReadId(storedName, out var id).Should().BeFalse();
        id.Should().Be(default(StoredFileId));
    }
}
