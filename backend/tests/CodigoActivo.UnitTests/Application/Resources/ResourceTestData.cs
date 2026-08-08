using System.Linq.Expressions;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.Resources;

internal static class ResourceTestData
{
    public const string SomeRichText =
        "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Contenido\"}]}]}";
    public const string EmptyRichText = "{\"type\":\"doc\",\"content\":[]}";

    public static ResourceType NewResourceType(bool isExternal = false, string? name = null)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name ?? (isExternal ? "Externo" : "Interno"),
            Description = isExternal ? "Recurso enlazado" : "Recurso propio",
            Color = "#3B82F6",
            IsExternal = isExternal,
        };
    }

    public static Resource NewResource(
        string title = "Guide",
        string subtitle = "Intro",
        int year = 2024,
        string? url = null,
        ResourceType? type = null
    )
    {
        var resourceType = type ?? NewResourceType();
        return new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = SomeRichText,
            Url = url,
            ResourceTypeId = resourceType.Id,
            ResourceType = resourceType,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static void HasResources(this IResourceRepository resources, params Resource[] items)
    {
        resources.Query().Returns(items.AsQueryable());
    }

    public static void HasTypes(
        this IResourceTypeRepository resourceTypes,
        params ResourceType[] items
    )
    {
        resourceTypes.Query().Returns(items.AsQueryable());
    }

    public static ResourceType TypeExists(
        this IResourceTypeRepository resourceTypes,
        bool isExternal = false
    )
    {
        var type = NewResourceType(isExternal);
        resourceTypes
            .FindAsync(
                Arg.Any<Expression<Func<ResourceType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(type);
        return type;
    }

    public static void TypeMissing(this IResourceTypeRepository resourceTypes)
    {
        ResourceType? missing = null;
        resourceTypes
            .FindAsync(
                Arg.Any<Expression<Func<ResourceType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(missing);
    }
}
