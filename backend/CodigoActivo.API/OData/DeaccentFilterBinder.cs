using Microsoft.AspNetCore.OData.Query.Expressions;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Linq.Expressions;
using System.Reflection;

namespace CodigoActivo.API.OData;

public sealed class DeaccentFilterBinder : FilterBinder
{
    public const string FunctionName = "deaccent";

    private static readonly MethodInfo ReplaceMethod = typeof(string).GetMethod(
        nameof(string.Replace),
        [typeof(string), typeof(string)]
    )!;

    private static readonly (string Accented, string Plain)[] Replacements =
    [
        ("á", "a"),
        ("é", "e"),
        ("í", "i"),
        ("ó", "o"),
        ("ú", "u"),
    ];

    private static bool FunctionRegistered;

    public static void EnsureFunctionRegistered()
    {
        if (FunctionRegistered)
        {
            return;
        }

        FunctionRegistered = true;
        var stringType = EdmCoreModel.Instance.GetString(isNullable: true);
        CustomUriFunctions.AddCustomUriFunction(
            FunctionName,
            new FunctionSignatureWithReturnType(stringType, stringType)
        );
    }

    public override Expression BindSingleValueFunctionCallNode(
        SingleValueFunctionCallNode node,
        QueryBinderContext context
    )
    {
        if (string.Equals(node.Name, FunctionName, StringComparison.Ordinal))
        {
            var argument = Bind(node.Parameters.Single(), context);
            return Fold(argument);
        }

        return base.BindSingleValueFunctionCallNode(node, context);
    }

    private static Expression Fold(Expression source)
    {
        var current = source;
        foreach (var (accented, plain) in Replacements)
        {
            current = Expression.Call(
                current,
                ReplaceMethod,
                Expression.Constant(accented),
                Expression.Constant(plain)
            );
        }

        return current;
    }
}
