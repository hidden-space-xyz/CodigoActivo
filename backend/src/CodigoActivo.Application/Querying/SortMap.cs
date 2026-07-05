using System.Linq.Expressions;

namespace CodigoActivo.Application.Querying;

public sealed class SortMap<T>
{
    private readonly Dictionary<string, LambdaExpression> selectors = new(
        StringComparer.OrdinalIgnoreCase
    );
    private IReadOnlyList<SortTerm> defaults = [];
    private LambdaExpression? tieBreaker;

    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        selectors[key] = selector;
        return this;
    }

    public SortMap<T> Default(params string[] terms)
    {
        defaults = terms.Select(Parse).Where(term => selectors.ContainsKey(term.Key)).ToList();
        return this;
    }

    public SortMap<T> Tie<TKey>(Expression<Func<T, TKey>> selector)
    {
        tieBreaker = selector;
        return this;
    }

    public IQueryable<T> Apply(IQueryable<T> source, string? sort)
    {
        var terms = ParseAll(sort).Where(term => selectors.ContainsKey(term.Key)).ToList();
        if (terms.Count == 0) terms = defaults.ToList();

        IOrderedQueryable<T>? ordered = null;
        foreach (var term in terms)
            ordered = ApplyOrder(ordered ?? source, selectors[term.Key], term.Descending, ordered is null);

        if (tieBreaker is not null)
            ordered = ApplyOrder(ordered ?? source, tieBreaker, descending: false, ordered is null);

        return ordered ?? source;
    }

    private static IOrderedQueryable<T> ApplyOrder(
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
        return (IOrderedQueryable<T>)source.Provider.CreateQuery<T>(call);
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
