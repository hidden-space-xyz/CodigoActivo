using System.Text.Json;

namespace CodigoActivo.Application.Caching;

public static class CacheKeys
{
    public static string For<TQuery>(string prefix, TQuery query)
    {
        return $"{prefix}:{JsonSerializer.Serialize(query)}";
    }
}
