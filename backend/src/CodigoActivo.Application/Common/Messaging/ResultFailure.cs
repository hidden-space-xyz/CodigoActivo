using System.Collections.Concurrent;
using System.Reflection;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Common.Messaging;

/// <summary>
/// Builds a failed <see cref="Result"/> or <see cref="Result{T}"/> for a handler contract whose
/// result type is only known as a type argument.
/// </summary>
internal static class ResultFailure
{
    private static readonly ConcurrentDictionary<Type, Func<Error, object>> Factories = new();

    /// <summary>
    /// Tells whether a result type can carry a failure.
    /// </summary>
    /// <typeparam name="TResult">Result type of a handler.</typeparam>
    /// <returns><see langword="true"/> for <see cref="Result"/> and <see cref="Result{T}"/>.</returns>
    public static bool IsSupported<TResult>()
    {
        return typeof(Result).IsAssignableFrom(typeof(TResult));
    }

    /// <summary>
    /// Creates the failed result of a handler.
    /// </summary>
    /// <typeparam name="TResult">Result type of the handler.</typeparam>
    /// <param name="error">Failure to report.</param>
    /// <returns>The failed result.</returns>
    public static TResult Of<TResult>(Error error)
    {
        return (TResult)Factories.GetOrAdd(typeof(TResult), FactoryFor)(error);
    }

    private static Func<Error, object> FactoryFor(Type resultType)
    {
        var conversion =
            resultType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(method =>
                    method.Name == "op_Implicit"
                    && method.ReturnType == resultType
                    && method.GetParameters() is [{ ParameterType: var parameter }]
                    && parameter == typeof(Error)
                )
            ?? throw new InvalidOperationException($"{resultType.Name} cannot carry a failure.");
        return error => conversion.Invoke(null, [error])!;
    }
}
