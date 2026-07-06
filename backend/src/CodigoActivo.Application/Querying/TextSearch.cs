using System.Linq.Expressions;
using System.Reflection;

namespace CodigoActivo.Application.Querying;

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

    public static string Normalize(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        foreach (var (accented, plain) in Folds)
            normalized = normalized.Replace(accented, plain, StringComparison.Ordinal);
        return normalized;
    }

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
            nameof(Querying.Term.Value)
        );
        body = Expression.Call(body, ContainsMethod, termAccess);
        return Expression.Lambda<Func<T, bool>>(body, selector.Parameters);
    }
}

internal sealed record Term(string Value);
