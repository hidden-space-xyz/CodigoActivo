using System.Linq.Expressions;
using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class RepositoryConventionTests
{
    private static List<Type> DomainRepositories()
    {
        return
        [
            .. typeof(Result)
                .Assembly.GetTypes()
                .Where(type =>
                    type is { IsInterface: true, IsGenericTypeDefinition: false }
                    && type.Name.EndsWith("Repository", StringComparison.Ordinal)
                ),
        ];
    }

    private static IEnumerable<MethodInfo> MethodsOf(Type repository)
    {
        return repository
            .GetInterfaces()
            .Prepend(repository)
            .SelectMany(contract => contract.GetMethods());
    }

    private static IEnumerable<Type> SignatureTypes(MethodInfo method)
    {
        return method
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Append(method.ReturnType);
    }

    private static bool ComposesQueries(Type type)
    {
        if (type.HasElementType)
        {
            return ComposesQueries(type.GetElementType()!);
        }

        return typeof(IQueryable).IsAssignableFrom(type)
            || typeof(Expression).IsAssignableFrom(type)
            || (type.IsGenericType && type.GetGenericArguments().Any(ComposesQueries));
    }

    [Fact]
    public void RepositoriesAlwaysStoreAnAggregateRoot()
    {
        var repositories = DomainRepositories();

        var offenders = repositories
            .Where(repository =>
                !repository
                    .GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IRepository<>)
                        && typeof(IAggregateRoot).IsAssignableFrom(
                            contract.GetGenericArguments()[0]
                        )
                    )
            )
            .Select(repository => repository.Name)
            .ToList();

        repositories.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void RepositoriesNeverAreDeclaredOutsideDomain()
    {
        var offenders = ProductionAssemblies
            .Types(
                ProductionAssemblies.Application,
                ProductionAssemblies.Infrastructure,
                ProductionAssemblies.Composition,
                ProductionAssemblies.Api
            )
            .Where(type =>
                type.IsInterface
                && type.Name.Split('`')[0].EndsWith("Repository", StringComparison.Ordinal)
            )
            .Select(type => type.FullName)
            .ToList();

        DomainRepositories().Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void DomainInterfacesImplementedOutsideDomainAlwaysAreRepositories()
    {
        var implemented = ProductionAssemblies
            .Types(
                ProductionAssemblies.Application,
                ProductionAssemblies.Infrastructure,
                ProductionAssemblies.Composition,
                ProductionAssemblies.Api
            )
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.Assembly == ProductionAssemblies.Domain)
            .Distinct()
            .ToList();

        var offenders = implemented
            .Where(contract =>
                !contract
                    .GetInterfaces()
                    .Prepend(contract)
                    .Any(candidate =>
                        candidate.IsGenericType
                        && candidate.GetGenericTypeDefinition() == typeof(IRepository<>)
                    )
            )
            .Select(contract => contract.Name)
            .ToList();

        implemented.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void RepositoriesNeverDeclareAnotherRemove()
    {
        var offenders = DomainRepositories()
            .SelectMany(MethodsOf)
            .Where(method =>
                method.Name.StartsWith("Remove", StringComparison.Ordinal)
                && !(
                    method.DeclaringType is { IsGenericType: true } contract
                    && contract.GetGenericTypeDefinition() == typeof(IRepository<>)
                )
            )
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void RepositoriesNeverExposeQueryablesOrExpressions()
    {
        var methods = DomainRepositories()
            .SelectMany(repository =>
                MethodsOf(repository).Select(method => (Repository: repository, Method: method))
            )
            .ToList();

        var offenders = methods
            .Where(entry => SignatureTypes(entry.Method).Any(ComposesQueries))
            .Select(entry => $"{entry.Repository.Name}.{entry.Method.Name}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        methods.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void QueryCompositionCheckAlwaysSpotsQueryablesAndExpressionsWrappedInOtherTypes()
    {
        ComposesQueries(typeof(IQueryable<User>)).Should().BeTrue();
        ComposesQueries(typeof(Task<IOrderedQueryable<User>>)).Should().BeTrue();
        ComposesQueries(typeof(Expression<Func<User, bool>>)).Should().BeTrue();
        ComposesQueries(typeof(Expression<Func<User, bool>>[])).Should().BeTrue();
        ComposesQueries(typeof(Task<IReadOnlyList<User>>)).Should().BeFalse();
        ComposesQueries(typeof(Func<User, bool>)).Should().BeFalse();
    }
}
