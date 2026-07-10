using AwesomeAssertions;
using CodigoActivo.Application.Querying;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Querying;

public sealed class SortMapTests
{
    private sealed record Row(int A, int B, int Id, string Name);

    private static SortMap<Row> FullMap() =>
        new SortMap<Row>().Add("a", r => r.A).Add("b", r => r.B).Default("a").Tie(r => r.Id);

    private static List<Row> Rows(params Row[] rows) => rows.ToList();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    public void Apply_SortMissingOrUnknown_FallsBackToDefaultThenTie(string? sort)
    {
        var rows = Rows(new Row(2, 0, 30, "x"), new Row(1, 0, 20, "y"), new Row(1, 0, 10, "z"));

        var ordered = FullMap().Apply(rows.AsQueryable(), sort).ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(10, 20, 30);
    }

    [Fact]
    public void Apply_SingleKey_OrdersAscendingThenTie()
    {
        var rows = Rows(new Row(3, 0, 1, "x"), new Row(1, 0, 2, "y"), new Row(2, 0, 3, "z"));

        var ordered = FullMap().Apply(rows.AsQueryable(), "a").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Apply_KeyPrefixedWithMinus_OrdersDescending()
    {
        var rows = Rows(new Row(1, 0, 1, "x"), new Row(3, 0, 2, "y"), new Row(2, 0, 3, "z"));

        var ordered = FullMap().Apply(rows.AsQueryable(), "-a").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(3, 2, 1);
    }

    [Fact]
    public void Apply_MultiKeySort_OrdersWithMixedDirections()
    {
        var rows = Rows(
            new Row(1, 5, 1, "x"),
            new Row(2, 1, 2, "y"),
            new Row(2, 9, 3, "z"),
            new Row(1, 2, 4, "w")
        );

        var ordered = FullMap().Apply(rows.AsQueryable(), "-a,b").ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(2, 3, 4, 1);
    }

    [Fact]
    public void Apply_UnknownKeysMixedWithKnown_IgnoresUnknownHonoursKnown()
    {
        var rows = Rows(new Row(2, 0, 1, "x"), new Row(1, 0, 2, "y"));

        var ordered = FullMap().Apply(rows.AsQueryable(), "nope,-a,other").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(2, 1);
    }

    [Fact]
    public void Apply_EqualKeys_AppendsTieBreakerForStableOrder()
    {
        var rows = Rows(
            new Row(7, 0, 40, "x"),
            new Row(7, 0, 10, "y"),
            new Row(7, 0, 30, "z"),
            new Row(7, 0, 20, "w")
        );

        var ordered = FullMap().Apply(rows.AsQueryable(), "a").ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(10, 20, 30, 40);
    }

    [Fact]
    public void Default_UnregisteredTerm_IsDropped()
    {
        var map = new SortMap<Row>().Add("a", r => r.A).Default("missing").Tie(r => r.Id);
        var rows = Rows(new Row(9, 0, 3, "x"), new Row(1, 0, 1, "y"), new Row(5, 0, 2, "z"));

        var ordered = map.Apply(rows.AsQueryable(), null).ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Apply_NoTermsDefaultsOrTie_ReturnsSourceUnordered()
    {
        var map = new SortMap<Row>().Add("a", r => r.A);
        var rows = Rows(new Row(3, 0, 3, "x"), new Row(1, 0, 1, "y"), new Row(2, 0, 2, "z"));

        var ordered = map.Apply(rows.AsQueryable(), null).ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(3, 1, 2);
    }

    [Fact]
    public void Apply_NoTieBreakerRegistered_OrdersWithoutTieBreaker()
    {
        var map = new SortMap<Row>().Add("a", r => r.A).Default("a");
        var rows = Rows(new Row(3, 0, 3, "x"), new Row(1, 0, 1, "y"), new Row(2, 0, 2, "z"));

        var ordered = map.Apply(rows.AsQueryable(), "-a").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(3, 2, 1);
    }
}
