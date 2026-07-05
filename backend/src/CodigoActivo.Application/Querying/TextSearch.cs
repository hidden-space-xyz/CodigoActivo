using System.Linq.Expressions;
using System.Reflection;

namespace CodigoActivo.Application.Querying;

/// <summary>
/// Accent- and case-insensitive text search, translated to SQL by EF Core. Reproduces the folding
/// the old OData <c>deaccent(tolower(field))</c> filter did: the column is lower-cased and the five
/// Spanish acute vowels are stripped (<c>LOWER</c> + chained <c>REPLACE</c>), then matched with
/// <c>LIKE</c>. Callers must fold the search term with <see cref="Normalize"/> first so both sides
/// use the same alphabet.
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

    /// <summary>Folds a raw search term to the same alphabet the column is folded to.</summary>
    public static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        foreach (var (accented, plain) in Folds)
            normalized = normalized.Replace(accented, plain, StringComparison.Ordinal);
        return normalized;
    }

    /// <summary>
    /// Builds <c>x =&gt; fold(selector(x)).Contains(term)</c> as an EF-translatable predicate. The
    /// term is captured (not inlined) so EF Core parameterizes it.
    /// </summary>
    public static Expression<Func<T, bool>> Contains<T>(
        Expression<Func<T, string?>> selector,
        string term
    )
    {
        // Coalesce to "" first: the selected column may be nullable (e.g. User.Email), and calling
        // ToLower() on a null throws under LINQ-to-Objects (the in-memory/fake executors) while
        // silently dropping the row under Postgres — folding null to "" makes both behave the same.
        Expression body = Expression.Call(
            Expression.Coalesce(selector.Body, Expression.Constant(string.Empty)),
            ToLowerMethod
        );
        foreach (var (accented, plain) in Folds)
            body = Expression.Call(
                body,
                ReplaceMethod,
                Expression.Constant(accented),
                Expression.Constant(plain)
            );

        var termAccess = Expression.Property(
            Expression.Constant(new Term(term)),
            nameof(Querying.Term.Value)
        );
        body = Expression.Call(body, ContainsMethod, termAccess);
        return Expression.Lambda<Func<T, bool>>(body, selector.Parameters);
    }
}

internal sealed record Term(string Value);
