using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CodigoActivo.IntegrationTests.Infrastructure;

internal static class TestDatabase
{
    /// <summary>
    /// Deletes every row of the model, referencing tables before the tables they reference, and
    /// restarts the identity sequences, in one round trip. A <c>DELETE</c> of a table holding a
    /// handful of rows costs far less than a <c>TRUNCATE</c>, which recreates the files of every
    /// table, index and log partition.
    /// </summary>
    public static async Task DeleteAllRowsAsync(CodigoActivoDbContext db)
    {
        var statements = DeletionOrder(db.Model)
            .Select(table => $"DELETE FROM {table};")
            .Append("SELECT setval(seqrelid, seqstart, false) FROM pg_sequence;");

        await db.Database.ExecuteSqlRawAsync(string.Join(' ', statements), TestCancellation.Ct);
    }

    private static List<string> DeletionOrder(IModel model)
    {
        var referencingTables = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entity in model.GetEntityTypes())
        {
            var table = TableOf(entity);
            if (table is null)
            {
                continue;
            }

            referencingTables.TryAdd(table, []);
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var principal = TableOf(foreignKey.PrincipalEntityType);
                if (principal is not null && principal != table)
                {
                    referencingTables.TryAdd(principal, []);
                    referencingTables[principal].Add(table);
                }
            }
        }

        var pending = referencingTables.Keys.ToHashSet(StringComparer.Ordinal);
        var ordered = new List<string>(pending.Count);
        while (pending.Count > 0)
        {
            var deletable = pending
                .Where(table => !referencingTables[table].Overlaps(pending))
                .Order(StringComparer.Ordinal)
                .ToList();
            if (deletable.Count is 0)
            {
                throw new InvalidOperationException(
                    "Foreign keys form a cycle between " + string.Join(", ", pending) + "."
                );
            }

            ordered.AddRange(deletable);
            pending.ExceptWith(deletable);
        }

        return ordered;
    }

    private static string? TableOf(IReadOnlyEntityType entity)
    {
        var table = entity.GetTableName();
        if (table is null)
        {
            return null;
        }

        var schema = entity.GetSchema();
        return schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";
    }
}
