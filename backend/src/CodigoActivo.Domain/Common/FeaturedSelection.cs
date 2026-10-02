namespace CodigoActivo.Domain.Common;

/// <summary>
/// Keeps a single featured item per kind of content: the items featured now stop being featured before the
/// chosen one is featured, so two items of the same kind are never featured at once.
/// </summary>
public static class FeaturedSelection
{
    /// <summary>
    /// Unfeatures every item currently featured except the chosen one, which keeps its state until it is
    /// featured.
    /// </summary>
    /// <typeparam name="T">Kind of content.</typeparam>
    /// <param name="chosen">Item to feature next.</param>
    /// <param name="featured">Items of the same kind featured right now.</param>
    public static void UnfeatureAllBut<T>(T chosen, IEnumerable<T> featured)
        where T : class, IFeaturable
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(featured);

        foreach (var other in featured.Where(other => !ReferenceEquals(other, chosen)))
        {
            other.Unfeature();
        }
    }
}
