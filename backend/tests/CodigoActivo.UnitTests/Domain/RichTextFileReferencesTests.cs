using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class RichTextFileReferencesTests
{
    [Fact]
    public void ContentUrlFileIdBuildsStoredUrlShape()
    {
        var id = StoredFileId.From(Guid.Parse("11111111-2222-3333-4444-555555555555"));

        RichTextFileReferences
            .ContentUrl(id)
            .Should()
            .Be("/api/files/11111111-2222-3333-4444-555555555555/content");
    }

    [Fact]
    public void IsContentUrlBuiltUrlReturnsTrue()
    {
        RichTextFileReferences
            .IsContentUrl(RichTextFileReferences.ContentUrl(StoredFileId.New()))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsContentUrlUppercaseIdentifierReturnsTrue()
    {
        RichTextFileReferences
            .IsContentUrl("/api/files/0F8FAD5B-D9CB-469F-A165-70867728950E/content")
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/api/files/not-a-guid/content")]
    [InlineData("/API/files/0f8fad5b-d9cb-469f-a165-70867728950e/content")]
    [InlineData("https://evil.example/api/files/0f8fad5b-d9cb-469f-a165-70867728950e/content")]
    [InlineData("/api/files/0f8fad5b-d9cb-469f-a165-70867728950e/content?x=1")]
    [InlineData("/api/files/0f8fad5b-d9cb-469f-a165-70867728950e/content\n")]
    public void IsContentUrlNotExactlyAContentUrlReturnsFalse(string value)
    {
        RichTextFileReferences.IsContentUrl(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"src\":\"/api/files/not-a-guid/content\"}")]
    public void ExtractNoValidReferenceEmbeddedReturnsEmpty(string? json)
    {
        RichTextFileReferences.Extract(RichText.FromOptional(json)).Should().BeEmpty();
    }

    [Fact]
    public void ExtractNoDocumentReturnsEmpty()
    {
        RichTextFileReferences.Extract(null).Should().BeEmpty();
    }

    [Fact]
    public void ExtractDuplicateAbsoluteAndUppercaseUrlsFindsIdsDeduped()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var json =
            $"{{\"img1\":\"/api/files/{a}/content\","
            + $"\"img2\":\"https://api.example.org/api/files/{b.ToString().ToUpperInvariant()}/content\","
            + $"\"again\":\"/api/files/{a}/content\"}}";

        RichTextFileReferences
            .Extract(RichText.From(json))
            .Should()
            .BeEquivalentTo([StoredFileId.From(a), StoredFileId.From(b)]);
    }

    [Fact]
    public void ExtractRemovedIdDroppedFromDocumentReturnsOnlyDroppedIds()
    {
        var removed = StoredFileId.New();
        var kept = StoredFileId.New();
        var previous = RichText.From(
            $"{{\"a\":\"{RichTextFileReferences.ContentUrl(removed)}\","
                + $"\"b\":\"{RichTextFileReferences.ContentUrl(kept)}\"}}"
        );
        var current = RichText.From($"{{\"b\":\"{RichTextFileReferences.ContentUrl(kept)}\"}}");

        RichTextFileReferences.ExtractRemoved(previous, current).Should().BeEquivalentTo([removed]);
    }
}
