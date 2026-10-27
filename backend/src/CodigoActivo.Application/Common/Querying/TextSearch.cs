using System.Linq.Expressions;
using System.Reflection;

namespace CodigoActivo.Application.Common.Querying;

/// <summary>
/// Matches search terms in the database without regard to case or to the acute accents of vowels.
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
    /// Trims a text, lowercases it and drops the acute accent of its vowels, the same folding
    /// <see cref="Contains{T}"/> applies to the text it searches.
    /// </summary>
    /// <param name="value">Search term, or a text to compare or order the same way.</param>
    /// <returns>The normalized text.</returns>
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
    /// Builds a predicate, translatable to SQL, that tells whether the selected text, lowercased with
    /// the acute accent of its vowels dropped, contains the term. A null text counts as empty, and
    /// the term is sent as a query parameter.
    /// </summary>
    /// <typeparam name="T">Type of the rows filtered.</typeparam>
    /// <param name="selector">Text of the row searched.</param>
    /// <param name="term">Term already passed through <see cref="Normalize"/>.</param>
    /// <returns>The predicate.</returns>
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
    /// Keeps the rows whose selected text contains the term passed through <see cref="Normalize"/>,
    /// as <see cref="Contains{T}"/> compares them.
    /// </summary>
    /// <typeparam name="T">Type of the rows filtered.</typeparam>
    /// <param name="source">Rows to filter.</param>
    /// <param name="selector">Text of the row searched.</param>
    /// <param name="term">Term typed by the client; null or blank keeps every row.</param>
    /// <returns>The matching rows.</returns>
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
