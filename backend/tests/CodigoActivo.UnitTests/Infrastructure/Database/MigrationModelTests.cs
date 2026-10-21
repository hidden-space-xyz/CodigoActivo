using AwesomeAssertions;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Database;

public sealed class MigrationModelTests
{
    [Fact]
    public void MigrationSnapshotAlwaysMatchesCurrentModel()
    {
        using var context = new CodigoActivoDesignTimeDbContextFactory().CreateDbContext([]);

        context
            .Database.HasPendingModelChanges()
            .Should()
            .BeFalse("a model change regenerates InitialCreate as CONTRIBUTING.md describes");
    }

    [Fact]
    public void MigrationsAlwaysAreTheSingleInitialCreate()
    {
        using var context = new CodigoActivoDesignTimeDbContextFactory().CreateDbContext([]);

        context
            .Database.GetMigrations()
            .Should()
            .ContainSingle("until the first deployment a model change regenerates InitialCreate")
            .Which.Should()
            .EndWith("_InitialCreate");
    }
}
