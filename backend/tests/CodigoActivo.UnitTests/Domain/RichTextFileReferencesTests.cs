using AwesomeAssertions;
using CodigoActivo.Domain.Storage;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class RichTextFileReferencesTests
{
    [Fact]
    public void ContentUrlMarker_builds_the_stored_url_shape()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        RichTextFileReferences
            .ContentUrlMarker(id)
            .Should()
            .Be("/api/files/11111111-2222-3333-4444-555555555555/content");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"src\":\"/api/files/not-a-guid/content\"}")]
    public void Extract_returns_empty_when_no_valid_reference_is_embedded(string? json)
    {
        RichTextFileReferences.Extract(json).Should().BeEmpty();
    }

    [Fact]
    public void Extract_finds_ids_dedupes_and_accepts_absolute_urls()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var json =
            $"{{\"img1\":\"/api/files/{a}/content\","
            + $"\"img2\":\"https://api.example.org/api/files/{b}/content\","
            + $"\"again\":\"/api/files/{a}/content\"}}";

        RichTextFileReferences.Extract(json).Should().BeEquivalentTo([a, b]);
    }

    [Fact]
    public void ExtractRemoved_returns_only_ids_dropped_from_the_document()
    {
        var removed = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var previous =
            $"{{\"a\":\"/api/files/{removed}/content\",\"b\":\"/api/files/{kept}/content\"}}";
        var current = $"{{\"b\":\"/api/files/{kept}/content\"}}";

        RichTextFileReferences.ExtractRemoved(previous, current).Should().BeEquivalentTo([removed]);
    }
}
