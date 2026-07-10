using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.IntegrationTests.Infrastructure;

internal static class TestDatabase
{
    public static async Task TruncateAllTablesAsync(CodigoActivoDbContext db)
    {
        var tables = db
            .Model.GetEntityTypes()
            .Select(entity => (Schema: entity.GetSchema(), Table: entity.GetTableName()))
            .Where(entity => entity.Table is not null)
            .Select(entity =>
                entity.Schema is null
                    ? $"\"{entity.Table}\""
                    : $"\"{entity.Schema}\".\"{entity.Table}\""
            )
            .Distinct()
            .ToList();

        if (tables.Count == 0)
            return;

        var sql = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
