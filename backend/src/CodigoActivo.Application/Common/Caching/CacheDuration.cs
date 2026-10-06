namespace CodigoActivo.Application.Common.Caching;

/// <summary>
/// How long a cached query result lives.
/// </summary>
public enum CacheDuration
{
    /// <summary>
    /// Immutable catalogs, kept for hours.
    /// </summary>
    Catalog,

    /// <summary>
    /// Dashboard aggregates, kept for a minute.
    /// </summary>
    Dashboard,
}
