using System.Linq.Expressions;

namespace CodigoActivo.Application.Common.Querying;

/// <summary>
/// Maps the sort keys a client sends, compared without regard to case, to the expressions that
/// order the rows.
/// </summary>
/// <typeparam name="T">Type of the rows sorted.</typeparam>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, LambdaExpression> selectors = new(
        StringComparer.OrdinalIgnoreCase
    );
    private IReadOnlyList<SortTerm> defaults = [];
    private readonly List<LambdaExpression> tieBreakers = [];

    /// <summary>
    /// Registers a sort key, replacing an earlier one of the same name.
    /// </summary>
    /// <typeparam name="TKey">Type of the value ordered by.</typeparam>
    /// <param name="key">Key the client names, without the <c>-</c> of descending order.</param>
    /// <param name="selector">Value of the row the key orders by.</param>
    /// <returns>This map, to chain further calls.</returns>
    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        selectors[key] = selector;
        return this;
    }

    /// <summary>
    /// Sets the sort terms used when the client names no known key. Call it after
    /// <see cref="Add{TKey}"/>: terms whose key is not registered yet are dropped.
    /// </summary>
    /// <param name="terms">Keys, each optionally prefixed with <c>-</c> for descending order.</param>
    /// <returns>This map, to chain further calls.</returns>
    public SortMap<T> Default(params string[] terms)
    {
        defaults = [.. terms.Select(Parse).Where(term => selectors.ContainsKey(term.Key))];
        return this;
    }

    /// <summary>
    /// Adds a deterministic tie-breaker to the sort order, after the ones added before.
    /// </summary>
    /// <typeparam name="TKey">Type of the value ordered by.</typeparam>
    /// <param name="selector">Value of the row ordered by, ascending, after every sort term.</param>
    /// <returns>This map, to chain further calls.</returns>
    public SortMap<T> Tie<TKey>(Expression<Func<T, TKey>> selector)
    {
        tieBreakers.Add(selector);
        return this;
    }

    /// <summary>
    /// Orders the rows by the known keys of the client, or by the defaults when it names none, and
    /// then by the tie-breakers. Unknown keys are ignored, and only the first occurrence of each
    /// known key is kept, so the client cannot grow the ordering, and with it the expression tree EF
    /// Core walks recursively, beyond one term per key.
    /// </summary>
    /// <param name="source">Rows to order.</param>
    /// <param name="sort">
    /// Comma-separated keys, each optionally prefixed with <c>-</c> for descending order; null or
    /// blank for the defaults.
    /// </param>
    /// <returns>The ordered rows, or <paramref name="source"/> when there is nothing to order by.</returns>
    public IQueryable<T> Apply(IQueryable<T> source, string? sort)
    {
        var terms = ParseAll(sort)
            .Where(term => selectors.ContainsKey(term.Key))
            .DistinctBy(term => term.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (terms.Count is 0)
        {
            terms = [.. defaults];
        }

        IQueryable<T>? ordered = null;
        foreach (var term in terms)
        {
            ordered = ApplyOrder(
                ordered ?? source,
                selectors[term.Key],
                term.Descending,
                ordered is null
            );
        }

        foreach (var tieBreaker in tieBreakers)
        {
            ordered = ApplyOrder(ordered ?? source, tieBreaker, descending: false, ordered is null);
        }

        return ordered ?? source;
    }

    private static IQueryable<T> ApplyOrder(
        IQueryable<T> source,
        LambdaExpression selector,
        bool descending,
        bool first
    )
    {
        var method = (first, descending) switch
        {
            (true, false) => nameof(Queryable.OrderBy),
            (true, true) => nameof(Queryable.OrderByDescending),
            (false, false) => nameof(Queryable.ThenBy),
            (false, true) => nameof(Queryable.ThenByDescending),
        };

        var call = Expression.Call(
            typeof(Queryable),
            method,
            [typeof(T), selector.ReturnType],
            source.Expression,
            Expression.Quote(selector)
        );
        return source.Provider.CreateQuery<T>(call);
    }

    private static IEnumerable<SortTerm> ParseAll(string? sort)
    {
        return string.IsNullOrWhiteSpace(sort)
            ? []
            : sort.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .Select(Parse);
    }

    private static SortTerm Parse(string term)
    {
        return term.StartsWith('-') ? new SortTerm(term[1..], true) : new SortTerm(term, false);
    }

    private readonly record struct SortTerm(string Key, bool Descending);
}
