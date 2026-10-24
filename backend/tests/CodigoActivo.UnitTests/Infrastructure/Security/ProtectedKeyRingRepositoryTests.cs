using System.Xml.Linq;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class ProtectedKeyRingRepositoryTests : IDisposable
{
    private const string DataProtectionNamespace = "http://schemas.asp.net/2015/03/dataProtection";

    private static readonly string AesGcmDecryptor =
        $"{typeof(AesGcmXmlDecryptor).FullName}, {typeof(AesGcmXmlDecryptor).Assembly.GetName().Name}, Version=1.0.0.0";

    private readonly IXmlRepository inner = Substitute.For<IXmlRepository>();
    private readonly RecordingLogger<ProtectedKeyRingRepository> logger = new();
    private readonly string directory = Path.Join(
        Path.GetTempPath(),
        "codigoactivo-keyring-tests",
        Guid.NewGuid().ToString("N")
    );

    private static XElement Key(params object[] secrets)
    {
        return new XElement(
            "key",
            new XAttribute("id", Guid.NewGuid()),
            new XAttribute("version", 1),
            new XElement(
                "descriptor",
                new XElement("descriptor", new XElement("encryption"), secrets)
            )
        );
    }

    private static XElement EncryptedSecret(string decryptorType)
    {
        return new XElement(
            XName.Get("encryptedSecret", DataProtectionNamespace),
            new XAttribute("decryptorType", decryptorType),
            new XElement("payload")
        );
    }

    private static XElement MasterKeyInClear(bool markedForEncryption = true)
    {
        return new XElement(
            "masterKey",
            markedForEncryption
                ? new XAttribute(XName.Get("requiresEncryption", DataProtectionNamespace), true)
                : null,
            new XElement("value", Convert.ToBase64String(new byte[32]))
        );
    }

    private IReadOnlyCollection<XElement> Read(params XElement[] elements)
    {
        inner.GetAllElements().Returns(elements);
        return new ProtectedKeyRingRepository(inner, logger).GetAllElements();
    }

    [Fact]
    public void GetAllElementsKeepsProtectedKeysAndElementsThatAreNotKeys()
    {
        var protectedKey = Key(EncryptedSecret(AesGcmDecryptor));
        var revocation = new XElement("revocation", new XElement("key"));

        Read(protectedKey, revocation).Should().Equal(protectedKey, revocation);

        logger.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(
        "Microsoft.AspNetCore.DataProtection.XmlEncryption.NullXmlDecryptor, Microsoft.AspNetCore.DataProtection"
    )]
    [InlineData("CodigoActivo.Infrastructure.Security.AesGcmXmlDecryptor, Planted")]
    [InlineData("CodigoActivo.Infrastructure.Security.AesGcmXmlDecryptor")]
    [InlineData("")]
    public void GetAllElementsLeavesOutAKeyWrappedByAnotherDecryptor(string decryptor)
    {
        Read(Key(EncryptedSecret(decryptor))).Should().BeEmpty();
    }

    [Fact]
    public void GetAllElementsLeavesOutKeysStoredInClear()
    {
        Read(Key(MasterKeyInClear()), Key(MasterKeyInClear(markedForEncryption: false)))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void GetAllElementsLeavesOutAClearKeyHiddenNextToAProtectedSecret()
    {
        Read(
                Key(MasterKeyInClear(markedForEncryption: false), EncryptedSecret(AesGcmDecryptor)),
                Key(EncryptedSecret(AesGcmDecryptor), EncryptedSecret("Other, Assembly"))
            )
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void GetAllElementsLogsHowManyKeysItLeftOut()
    {
        Read(
            Key(EncryptedSecret(AesGcmDecryptor)),
            Key(MasterKeyInClear()),
            Key(EncryptedSecret(string.Empty))
        );

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("Ignored 2 data protection keys not encrypted with DATA_PROTECTION_KEY");
    }

    [Fact]
    public void StoreElementDelegatesToTheInnerRepository()
    {
        var element = Key(EncryptedSecret(AesGcmDecryptor));

        new ProtectedKeyRingRepository(inner, logger).StoreElement(element, "friendly");

        inner.Received(1).StoreElement(element, "friendly");
    }

    [Fact]
    public void ProtectKeysWithRootKeyIgnoresAKeyPlantedWithoutTheRootKey()
    {
        var rootKey = DataProtectionRootKeys.Random();
        Guid protectedKeyId;
        using (var provider = BuildProtectedProvider(rootKey))
        {
            protectedKeyId = provider
                .GetRequiredService<IKeyManager>()
                .CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90))
                .KeyId;
        }

        Guid plantedKeyId;
        using (var rogue = BuildRogueProvider())
        {
            plantedKeyId = rogue
                .GetRequiredService<IKeyManager>()
                .CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(180))
                .KeyId;
        }

        using var reloaded = BuildProtectedProvider(rootKey);
        var keyIds = reloaded
            .GetRequiredService<IKeyManager>()
            .GetAllKeys()
            .Select(key => key.KeyId)
            .ToList();

        keyIds.Should().Contain(protectedKeyId).And.NotContain(plantedKeyId);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private ServiceProvider BuildProtectedProvider(DataProtectionRootKey rootKey)
    {
        var services = new ServiceCollection();
        services.AddSingleton(rootKey);
        services.AddSingleton(new KeyRingOptions { DirectoryPath = directory });
        services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .ProtectPayloadsWithAesGcm()
            .ProtectKeysWithRootKey();
        return services.BuildServiceProvider();
    }

    private ServiceProvider BuildRogueProvider()
    {
        var services = new ServiceCollection();
        services
            .AddDataProtection()
            .SetApplicationName("CodigoActivo")
            .PersistKeysToFileSystem(new DirectoryInfo(directory));
        services.Configure<KeyManagementOptions>(options =>
            options.XmlEncryptor = new NullXmlEncryptor()
        );
        return services.BuildServiceProvider();
    }
}
