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
            .BeFalse("every model change adds a migration that keeps the deployed data");
    }
}
