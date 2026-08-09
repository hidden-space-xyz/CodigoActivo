using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Communication;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class LayerReferenceTests
{
    private static readonly string[] ForbiddenForApplication =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "MailKit",
        "MimeKit",
        "Serilog",
        "Swashbuckle",
        "CodigoActivo.Infrastructure",
        "CodigoActivo.Composition",
        "CodigoActivo.API",
    ];

    private static List<string> ReferencedAssemblyNames(Assembly assembly)
    {
        return
        [
            .. assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty),
        ];
    }

    [Fact]
    public void ApplicationAssemblyReferencesNeverIncludePersistenceWebOrOuterLayers()
    {
        var references = ReferencedAssemblyNames(typeof(IQuery<>).Assembly);

        references
            .Should()
            .NotContain(name =>
                ForbiddenForApplication.Any(prefix =>
                    name.StartsWith(prefix, StringComparison.Ordinal)
                )
            );
    }

    [Fact]
    public void DomainAssemblyReferencesAreBclOnly()
    {
        var references = ReferencedAssemblyNames(typeof(Result).Assembly);

        references
            .Should()
            .OnlyContain(name =>
                name.StartsWith("System", StringComparison.Ordinal)
                || string.Equals(name, "netstandard", StringComparison.Ordinal)
                || string.Equals(name, "mscorlib", StringComparison.Ordinal)
            );
    }

    [Fact]
    public void InfrastructureAssemblyReferencesNeverIncludeApplication()
    {
        var references = ReferencedAssemblyNames(typeof(SmtpEmailSender).Assembly);

        references.Should().NotContain("CodigoActivo.Application");
    }

    [Fact]
    public void ControllerSignaturesCodigoActivoTypesComeFromApplicationOrDomainCommon()
    {
        var controllers = typeof(ApiErrorResponseExtensions)
            .Assembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && typeof(ControllerBase).IsAssignableFrom(type)
            )
            .ToList();

        var offenders = controllers
            .SelectMany(SignatureTypes)
            .Where(type =>
                type.Namespace is { } ns
                && ns.StartsWith("CodigoActivo", StringComparison.Ordinal)
                && !IsAllowedInControllerSignature(ns)
            )
            .Select(type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        controllers.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    private static IEnumerable<Type> SignatureTypes(Type controller)
    {
        var constructorParameters = controller
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        var actionTypes = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(action =>
                action
                    .GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Append(action.ReturnType)
            );

        return constructorParameters.Concat(actionTypes).SelectMany(Flatten);
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.IsGenericParameter)
        {
            yield break;
        }

        if (!type.IsGenericType)
        {
            yield return type;
            yield break;
        }

        yield return type.GetGenericTypeDefinition();

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in Flatten(argument))
            {
                yield return nested;
            }
        }
    }

    private static bool IsAllowedInControllerSignature(string ns)
    {
        return ns.StartsWith("CodigoActivo.Application", StringComparison.Ordinal)
            || string.Equals(ns, "CodigoActivo.Domain.Common", StringComparison.Ordinal);
    }
}
