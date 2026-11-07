using AwesomeAssertions;
using CodigoActivo.Composition.Configuration;
using CodigoActivo.Composition.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class DatabaseConnectionStringTests
{
    [Fact]
    public void BuildConnectionStringPasswordLoginDisablesGssEncryption()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["POSTGRES_HOST"] = "db",
                    ["POSTGRES_PASSWORD"] = "secret",
                }
            )
            .Build();

        var connection = new NpgsqlConnectionStringBuilder(
            PersistenceRegistration.BuildConnectionString(new Settings(configuration))
        );

        connection.GssEncryptionMode.Should().Be(GssEncryptionMode.Disable);
    }
}
