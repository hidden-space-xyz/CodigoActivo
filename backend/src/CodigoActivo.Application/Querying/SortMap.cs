using System.Linq.Expressions;

namespace CodigoActivo.Application.Querying;

/// <summary>
/// Maps client sort keys to strongly typed query expressions.
/// </summary>
/// <typeparam name="T">Type of item processed by the operation.</typeparam>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, LambdaExpression> selectors = new(
        StringComparer.OrdinalIgnoreCase
    );
    private IReadOnlyList<SortTerm> defaults = [];
    private LambdaExpression? tieBreaker;

    /// <summary>
    /// Adds a sort map to the current unit of work.
    /// </summary>
    /// <typeparam name="TKey">Type used for key.</typeparam>
    /// <param name="key">The key value.</param>
    /// <param name="selector">The selector value.</param>
    /// <returns>The resulting t value.</returns>
    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        selectors[key] = selector;
        return this;
    }

    /// <summary>
    /// Sets the fallback sort expression used for unknown sort keys.
    /// </summary>
    /// <param name="terms">The terms value.</param>
    /// <returns>The resulting t value.</returns>
    public SortMap<T> Default(params string[] terms)
    {
        defaults = [.. terms.Select(Parse).Where(term => selectors.ContainsKey(term.Key))];
        return this;
    }

    /// <summary>
    /// Adds a deterministic tie-breaker to the sort order.
    /// </summary>
    /// <typeparam name="TKey">Type used for key.</typeparam>
    /// <param name="selector">The selector value.</param>
    /// <returns>The resulting t value.</returns>
    public SortMap<T> Tie<TKey>(Expression<Func<T, TKey>> selector)
    {
        tieBreaker = selector;
        return this;
    }

    /// <summary>
    /// Applies the sort map rules to the supplied target.
    /// </summary>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="sort">The sort value.</param>
    /// <returns>The resulting t value.</returns>
    public IQueryable<T> Apply(IQueryable<T> source, string? sort)
    {
        var terms = ParseAll(sort).Where(term => selectors.ContainsKey(term.Key)).ToList();
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

        if (tieBreaker is not null)
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
