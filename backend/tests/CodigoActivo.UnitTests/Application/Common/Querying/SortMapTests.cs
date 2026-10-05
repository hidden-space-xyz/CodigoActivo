using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Common.Querying;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Querying;

public sealed class SortMapTests
{
    private sealed record Row(int A, int B, int Id, string Name);

    private static SortMap<Row> FullMap()
    {
        return new SortMap<Row>().Add("a", r => r.A).Add("b", r => r.B).Default("a").Tie(r => r.Id);
    }

    private static List<Row> Rows(params Row[] rows)
    {
        return [.. rows];
    }

    private static List<string> OrderingCalls(Expression expression)
    {
        var calls = new List<string>();
        while (expression is MethodCallExpression call)
        {
            calls.Insert(0, call.Method.Name);
            expression = call.Arguments[0];
        }

        return calls;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    public void ApplySortMissingOrUnknownFallsBackToDefaultThenTie(string? sort)
    {
        var rows = Rows(new Row(2, 0, 30, "x"), new Row(1, 0, 20, "y"), new Row(1, 0, 10, "z"));

        var ordered = FullMap().Apply(rows.AsQueryable(), sort).ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(10, 20, 30);
    }

    [Fact]
    public void ApplySingleKeyOrdersAscendingThenTie()
    {
        var rows = Rows(new Row(3, 0, 1, "x"), new Row(1, 0, 2, "y"), new Row(2, 0, 3, "z"));

        var ordered = FullMap().Apply(rows.AsQueryable(), "a").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void ApplyKeyPrefixedWithMinusOrdersDescending()
    {
        var rows = Rows(new Row(1, 0, 1, "x"), new Row(3, 0, 2, "y"), new Row(2, 0, 3, "z"));

        var ordered = FullMap().Apply(rows.AsQueryable(), "-a").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(3, 2, 1);
    }

    [Fact]
    public void ApplyMultiKeySortOrdersWithMixedDirections()
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
    public void ApplyUnknownKeysMixedWithKnownIgnoresUnknownHonoursKnown()
    {
        var rows = Rows(new Row(2, 0, 1, "x"), new Row(1, 0, 2, "y"));

        var ordered = FullMap().Apply(rows.AsQueryable(), "nope,-a,other").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(2, 1);
    }

    [Fact]
    public void ApplyRepeatedKeysKeepOnlyTheFirstOccurrenceOfEachKey()
    {
        var rows = Rows(
            new Row(1, 5, 1, "x"),
            new Row(2, 1, 2, "y"),
            new Row(2, 9, 3, "z"),
            new Row(1, 2, 4, "w")
        );
        var sort = string.Join(",", Enumerable.Repeat("-a,a,A,b,-B", 5_000));

        var query = FullMap().Apply(rows.AsQueryable(), sort);

        OrderingCalls(query.Expression)
            .Should()
            .Equal(
                nameof(Queryable.OrderByDescending),
                nameof(Queryable.ThenBy),
                nameof(Queryable.ThenBy)
            );
        query.ToList().Select(r => r.Id).Should().ContainInOrder(2, 3, 4, 1);
    }

    [Fact]
    public void ApplyEqualKeysAppendsTieBreakerForStableOrder()
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
    public void DefaultUnregisteredTermIsDropped()
    {
        var map = new SortMap<Row>().Add("a", r => r.A).Default("missing").Tie(r => r.Id);
        var rows = Rows(new Row(9, 0, 3, "x"), new Row(1, 0, 1, "y"), new Row(5, 0, 2, "z"));

        var ordered = map.Apply(rows.AsQueryable(), null).ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void ApplyNoTermsDefaultsOrTieReturnsSourceUnordered()
    {
        var map = new SortMap<Row>().Add("a", r => r.A);
        var rows = Rows(new Row(3, 0, 3, "x"), new Row(1, 0, 1, "y"), new Row(2, 0, 2, "z"));

        var ordered = map.Apply(rows.AsQueryable(), null).ToList();

        ordered.Select(r => r.Id).Should().ContainInOrder(3, 1, 2);
    }

    [Fact]
    public void ApplyNoTieBreakerRegisteredOrdersWithoutTieBreaker()
    {
        var map = new SortMap<Row>().Add("a", r => r.A).Default("a");
        var rows = Rows(new Row(3, 0, 3, "x"), new Row(1, 0, 1, "y"), new Row(2, 0, 2, "z"));

        var ordered = map.Apply(rows.AsQueryable(), "-a").ToList();

        ordered.Select(r => r.A).Should().ContainInOrder(3, 2, 1);
    }
}
