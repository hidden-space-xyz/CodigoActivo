using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Composition;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class DataHistoryWiringTests : IDisposable
{
    private readonly string keysDirectory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-keyring-tests",
        Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        if (Directory.Exists(keysDirectory))
        {
            Directory.Delete(keysDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task AddCodigoActivoAttachesTheScopeRecorderToTheWriteContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["SMTP_HOST"] = "smtp.example.test",
                    ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
                    ["DATA_PROTECTION_KEY"] = DataProtectionRootKeys.RandomBase64(),
                }
            )
            .Build();
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .AddSingleton(new KeyRingOptions { DirectoryPath = keysDirectory })
            .AddScoped(_ => Substitute.For<ICurrentUser>())
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<CodigoActivoDbContext>();

        context
            .GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()!
            .Interceptors.Should()
            .ContainSingle(
                interceptor => interceptor is DataHistoryRecorder,
                "a context the container builds never commits a historized change without its history"
            )
            .Which.Should()
            .BeSameAs(scope.ServiceProvider.GetRequiredService<DataHistoryRecorder>());
    }
}
