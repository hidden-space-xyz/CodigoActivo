using AwesomeAssertions;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Database;

public sealed class ReadModelMappingTests : IDisposable
{
    private readonly CodigoActivoDbContext writeContext = new(
        new DbContextOptionsBuilder<CodigoActivoDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options
    );

    private readonly CodigoActivoReadDbContext readContext = new(
        new DbContextOptionsBuilder<CodigoActivoReadDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options
    );

    public void Dispose()
    {
        writeContext.Dispose();
        readContext.Dispose();
    }

    [Fact]
    public void ReadModelColumnsAlwaysExistInWriteModelWithSameType()
    {
        var writeColumns = ColumnsOf(writeContext.Model);

        var mismatches = ColumnsOf(readContext.Model)
            .Where(column =>
                !writeColumns.TryGetValue(column.Key, out var written)
                || written.Type != column.Value.Type
                || (written.Nullable && !column.Value.Nullable)
            )
            .Select(column => column.Key);

        mismatches.Should().BeEmpty();
    }

    [Fact]
    public void ReadModelKeysAlwaysMatchWriteModelPrimaryKeys()
    {
        var writeKeys = KeysOf(writeContext.Model);

        KeysOf(readContext.Model).Should().BeSubsetOf(writeKeys);
    }

    [Fact]
    public void ReadModelTablesAlwaysAreExcludedFromMigrations()
    {
        readContext
            .GetService<IDesignTimeModel>()
            .Model.GetEntityTypes()
            .Should()
            .NotBeEmpty()
            .And.OnlyContain(entityType => entityType.IsTableExcludedFromMigrations());
    }

    private static Dictionary<string, (Type Type, bool Nullable)> ColumnsOf(IModel model)
    {
        return model
            .GetEntityTypes()
            .SelectMany(entityType =>
            {
                var table = StoreObjectIdentifier.Table(entityType.GetTableName()!);
                return entityType
                    .GetProperties()
                    .Select(property =>
                        (
                            Key: $"{table.Name}.{property.GetColumnName(table)}",
                            Type: StoredType(property),
                            property.IsNullable
                        )
                    );
            })
            .ToDictionary(column => column.Key, column => (column.Type, column.IsNullable));
    }

    private static Type StoredType(IProperty property)
    {
        var type =
            property.GetValueConverter()?.ProviderClrType
            ?? property.GetProviderClrType()
            ?? property.ClrType;
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static IEnumerable<string> KeysOf(IModel model)
    {
        return model
            .GetEntityTypes()
            .Select(entityType =>
            {
                var table = StoreObjectIdentifier.Table(entityType.GetTableName()!);
                var columns = entityType
                    .FindPrimaryKey()!
                    .Properties.Select(property => property.GetColumnName(table))
                    .Order(StringComparer.Ordinal);
                return $"{table.Name}({string.Join(',', columns)})";
            });
    }
}
