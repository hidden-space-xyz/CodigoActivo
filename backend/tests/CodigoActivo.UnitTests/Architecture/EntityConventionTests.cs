using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class EntityConventionTests : IDisposable
{
    private readonly CodigoActivoDbContext context = new(
        new DbContextOptionsBuilder<CodigoActivoDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options
    );

    public void Dispose()
    {
        context.Dispose();
    }

    private List<Type> DomainEntities()
    {
        return
        [
            .. context
                .Model.GetEntityTypes()
                .Where(entity =>
                    !entity.IsOwned() && entity.ClrType.Assembly == ProductionAssemblies.Domain
                )
                .Select(entity => entity.ClrType),
        ];
    }

    [Fact]
    public void NavigationsNeverTargetAnAggregateRoot()
    {
        var navigations = context
            .Model.GetEntityTypes()
            .SelectMany(entity =>
                entity
                    .GetDeclaredNavigations()
                    .Cast<INavigationBase>()
                    .Concat(entity.GetDeclaredSkipNavigations())
            )
            .ToList();

        var offenders = navigations
            .Where(navigation =>
                typeof(IAggregateRoot).IsAssignableFrom(navigation.TargetEntityType.ClrType)
            )
            .Select(navigation =>
                $"{navigation.DeclaringEntityType.ClrType.Name}.{navigation.Name}"
            )
            .ToList();

        navigations.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void ChildEntitiesNeverExposePublicConstructorsOrMethods()
    {
        var children = DomainEntities()
            .Where(type => !typeof(IAggregateRoot).IsAssignableFrom(type))
            .ToList();

        var offenders = children
            .SelectMany(type =>
                type.GetConstructors()
                    .Cast<MethodBase>()
                    .Concat(
                        type.GetMethods()
                            .Where(method =>
                                !method.IsSpecialName
                                && method.GetBaseDefinition().DeclaringType != typeof(object)
                            )
                    )
                    .Select(member => $"{type.Name}.{member.Name}")
            )
            .ToList();

        children.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void DomainEntitiesNeverExposePublicSetters()
    {
        var entities = DomainEntities();

        var offenders = entities
            .SelectMany(type =>
                type.GetProperties()
                    .Where(property => property.SetMethod is { IsPublic: true })
                    .Select(property => $"{type.Name}.{property.Name}")
            )
            .ToList();

        entities.Should().NotBeEmpty();
        offenders.Should().BeEmpty();
    }
}
