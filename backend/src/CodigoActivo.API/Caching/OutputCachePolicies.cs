namespace CodigoActivo.API.Caching;

/// <summary>
/// Names the output cache policies that are not a single cache tag; each cache tag of
/// <c>CacheTags.OutputCached</c> is a policy of its own.
/// </summary>
public static class OutputCachePolicies
{
    /// <summary>
    /// Policy of the sitemap and <c>robots.txt</c>, evicted when events, news or resources change.
    /// </summary>
    public const string Seo = "seo";
}
