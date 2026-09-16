using System.Linq.Expressions;
using System.Reflection;

namespace CodigoActivo.Application.Querying;

/// <summary>
/// Normalizes search terms and applies database text matching.
/// </summary>
public static class TextSearch
{
    private static readonly (string Accented, string Plain)[] Folds =
    [
        ("á", "a"),
        ("é", "e"),
        ("í", "i"),
        ("ó", "o"),
        ("ú", "u"),
    ];

    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(
        nameof(string.ToLower),
        Type.EmptyTypes
    )!;

    private static readonly MethodInfo ReplaceMethod = typeof(string).GetMethod(
        nameof(string.Replace),
        [typeof(string), typeof(string)]
    )!;

    private static readonly MethodInfo ContainsMethod = typeof(string).GetMethod(
        nameof(string.Contains),
        [typeof(string)]
    )!;

    /// <summary>
    /// Normalizes a search term for case-insensitive matching.
    /// </summary>
    /// <param name="value">Value to validate or convert.</param>
    /// <returns>The generated text.</returns>
    public static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        foreach (var (accented, plain) in Folds)
        {
            normalized = normalized.Replace(accented, plain, StringComparison.Ordinal);
        }

        return normalized;
    }

    /// <summary>
    /// Determines whether the normalized source contains the search term.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="selector">The selector value.</param>
    /// <param name="term">The term value.</param>
    /// <returns>The resulting t, bool value.</returns>
    public static Expression<Func<T, bool>> Contains<T>(
        Expression<Func<T, string?>> selector,
        string term
    )
    {
        Expression body = Expression.Call(
            Expression.Coalesce(selector.Body, Expression.Constant(string.Empty)),
            ToLowerMethod
        );
        foreach (var (accented, plain) in Folds)
        {
            body = Expression.Call(
                body,
                ReplaceMethod,
                Expression.Constant(accented),
                Expression.Constant(plain)
            );
        }

        var termAccess = Expression.Property(
            Expression.Constant(new Term(term)),
            nameof(Term.Value)
        );
        body = Expression.Call(body, ContainsMethod, termAccess);
        return Expression.Lambda<Func<T, bool>>(body, selector.Parameters);
    }

    /// <summary>
    /// Filters the source to rows where contains.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="selector">The selector value.</param>
    /// <param name="term">The term value.</param>
    /// <returns>The resulting t value.</returns>
    public static IQueryable<T> WhereContains<T>(
        this IQueryable<T> source,
        Expression<Func<T, string?>> selector,
        string? term
    )
    {
        return string.IsNullOrWhiteSpace(term)
            ? source
            : source.Where(Contains(selector, Normalize(term)));
    }

    private sealed record Term(string Value);
}
