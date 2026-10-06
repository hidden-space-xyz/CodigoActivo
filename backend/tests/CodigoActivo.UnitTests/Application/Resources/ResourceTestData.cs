using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Domain.Resources;
using CodigoActivo.UnitTests.TestSupport;

namespace CodigoActivo.UnitTests.Application.Resources;

internal static class ResourceTestData
{
    public const string SomeRichText =
        "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Contenido\"}]}]}";
    public const string EmptyRichText = "{\"type\":\"doc\",\"content\":[]}";

    public static ResourceTypeRow NewResourceTypeRow(bool isExternal = false, string? name = null)
    {
        return new()
        {
            Id = isExternal ? KnownIds.ResourceTypes.External : KnownIds.ResourceTypes.Internal,
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
        ResourceType type = ResourceType.Internal,
        string description = SomeRichText
    )
    {
        return Persisted.As<Resource>(
            new
            {
                Id = Guid.NewGuid(),
                Title = title,
                Subtitle = subtitle,
                Description = description,
                Url = url,
                ResourceType = type,
                ThumbnailId = Guid.NewGuid(),
                CreatedAt = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = Guid.NewGuid(),
            }
        );
    }

    public static ResourceRow NewResourceRow(
        string title = "Guide",
        string subtitle = "Intro",
        int year = 2024,
        string? url = null,
        ResourceTypeRow? type = null
    )
    {
        var resourceType = type ?? NewResourceTypeRow();
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

    public static ResourceTypeRow TypeExists(this FakeReadStore readStore, bool isExternal = false)
    {
        var type = NewResourceTypeRow(isExternal);
        readStore.ResourceTypes.Add(type);
        return type;
    }

    public static void TypeMissing(this FakeReadStore readStore)
    {
        readStore.ResourceTypes.Add(NewResourceTypeRow());
    }
}
