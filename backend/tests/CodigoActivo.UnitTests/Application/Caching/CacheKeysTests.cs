using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Caching;

public sealed class CacheKeysTests
{
    [Fact]
    public void ForEquivalentQueriesReturnsSameBoundedKeyWithoutEmbeddingInput()
    {
        var sensitiveMarker = new string('x', 2_000);
        var first = CacheKeys.For("items:list", new { Search = sensitiveMarker, Page = 1 });
        var second = CacheKeys.For("items:list", new { Search = sensitiveMarker, Page = 1 });

        first.Should().Be(second);
        first.Should().StartWith("items:list:");
        first.Should().HaveLength("items:list:".Length + 64);
        first.Should().NotContain(sensitiveMarker);
    }

    [Fact]
    public void ForDifferentQueriesReturnsDifferentKeys()
    {
        var first = CacheKeys.For("items:list", new { Search = "first", Page = 1 });
        var second = CacheKeys.For("items:list", new { Search = "second", Page = 1 });

        first.Should().NotBe(second);
    }
}
