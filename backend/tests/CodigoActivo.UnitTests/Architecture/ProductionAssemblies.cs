using System.Reflection;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication;

namespace CodigoActivo.UnitTests.Architecture;

internal static class ProductionAssemblies
{
    internal static Assembly Domain { get; } = typeof(Result).Assembly;

    internal static Assembly Application { get; } = typeof(IQuery<>).Assembly;

    internal static Assembly Infrastructure { get; } = typeof(SmtpEmailSender).Assembly;

    internal static Assembly Composition { get; } = typeof(DependencyInjection).Assembly;

    internal static Assembly Api { get; } = typeof(ApiErrorResponseExtensions).Assembly;

    internal static IReadOnlyList<Assembly> All { get; } =
    [Domain, Application, Infrastructure, Composition, Api];

    internal static IEnumerable<Type> AllTypes => Types(All);

    internal static IEnumerable<Type> Types(params IEnumerable<Assembly> assemblies)
    {
        return assemblies.SelectMany(assembly => assembly.GetTypes());
    }
}
