using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Database;

public sealed class FileReferenceCoverageTests : IDisposable
{
    private const string Decide =
        "every column that shows a stored file must be checked by StoredFileRepository.GetInUseAsync "
        + "and covered by its repository tests, or the file can be deleted while it is still shown";

    private const string Restrict =
        "every column that holds a stored file identifier needs "
        + "HasOne<StoredFile>().WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Restrict)";

    private static readonly string[] CheckedFileIdColumns =
    [
        "activities.thumbnail_id",
        "events.thumbnail_id",
        "news.thumbnail_id",
        "partners.thumbnail_id",
        "resources.thumbnail_id",
    ];

    private static readonly string[] CheckedRichTextColumns =
    [
        "events.description",
        "news.description",
        "resources.description",
        "terms_documents.description",
        "terms_document_versions.description",
    ];

    private readonly CodigoActivoDbContext context = new(
        new DbContextOptionsBuilder<CodigoActivoDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .UseSnakeCaseNamingConvention()
            .Options
    );

    public void Dispose()
    {
        context.Dispose();
    }

    private IEnumerable<IProperty> PropertiesHolding(Type type)
    {
        return context
            .Model.GetEntityTypes()
            .Where(entity => entity.ClrType != typeof(StoredFile))
            .SelectMany(entity => entity.GetFlattenedDeclaredProperties())
            .Where(property =>
                Holds(property.ClrType, type) || Holds(property.GetElementType()?.ClrType, type)
            );
    }

    private static bool Holds(Type? clrType, Type type)
    {
        return clrType is not null && (Nullable.GetUnderlyingType(clrType) ?? clrType) == type;
    }

    private static string ColumnOf(IProperty property)
    {
        return $"{property.DeclaringType.ContainingEntityType.GetTableName()}.{property.GetColumnName()}";
    }

    [Fact]
    public void ModelStoredFileIdColumnsAreCheckedForUse()
    {
        PropertiesHolding(typeof(StoredFileId))
            .Select(ColumnOf)
            .Should()
            .BeEquivalentTo(CheckedFileIdColumns, Decide);
    }

    [Fact]
    public void ModelRichTextColumnsAreCheckedForUse()
    {
        PropertiesHolding(typeof(RichText))
            .Select(ColumnOf)
            .Should()
            .BeEquivalentTo(CheckedRichTextColumns, Decide);
    }

    [Fact]
    public void ModelForeignKeysToFilesRestrictTheirDeletion()
    {
        var foreignKeys = context
            .Model.FindEntityType(typeof(StoredFile))!
            .GetReferencingForeignKeys()
            .ToList();

        foreignKeys
            .SelectMany(foreignKey => foreignKey.Properties)
            .Select(ColumnOf)
            .Should()
            .BeEquivalentTo(CheckedFileIdColumns, Restrict);
        foreignKeys
            .Should()
            .AllSatisfy(foreignKey =>
                foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict, Restrict)
            );
    }
}
