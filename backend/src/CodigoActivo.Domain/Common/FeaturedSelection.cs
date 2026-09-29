namespace CodigoActivo.Domain.Common;

/// <summary>
/// Keeps a single featured item per kind of content: featuring one stops featuring the rest.
/// </summary>
public static class FeaturedSelection
{
    /// <summary>
    /// Features the chosen item and unfeatures every other one currently featured.
    /// </summary>
    /// <typeparam name="T">Kind of content.</typeparam>
    /// <param name="chosen">Item to feature.</param>
    /// <param name="featured">Items of the same kind featured right now.</param>
    public static void Choose<T>(T chosen, IEnumerable<T> featured)
        where T : class, IFeaturable
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(featured);

        foreach (var other in featured.Where(other => !ReferenceEquals(other, chosen)))
        {
            other.Unfeature();
        }

        chosen.Feature();
    }
}
