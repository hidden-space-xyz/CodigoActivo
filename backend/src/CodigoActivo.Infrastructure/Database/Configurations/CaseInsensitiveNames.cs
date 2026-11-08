using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Comparison of the names that are unique ignoring letter case, such as those of event category
/// types and terms documents. Their unique index and the repository check that a name is free
/// both compare with <see cref="Collation"/>, so they agree on which names are taken.
/// </summary>
internal static class CaseInsensitiveNames
{
    /// <summary>
    /// Nondeterministic ICU collation that tells text apart by its letters and accents but not by
    /// their case.
    /// </summary>
    public const string Collation = "case_insensitive";

    /// <summary>
    /// Declares <see cref="Collation"/> in the database.
    /// </summary>
    /// <param name="modelBuilder">Builder of the model that uses the collation.</param>
    public static void HasCaseInsensitiveCollation(this ModelBuilder modelBuilder)
    {
        modelBuilder.HasCollation(
            Collation,
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false
        );
    }
}
