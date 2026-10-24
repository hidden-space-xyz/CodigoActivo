using System.Security.Cryptography;
using System.Xml.Linq;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class AesGcmKeyRingProtectionTests
{
    private const string ElementNamespace = "urn:codigoactivo:data-protection:aes-gcm:v1";
    private const int ElementVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static readonly XNamespace Namespace = XNamespace.Get(ElementNamespace);

    private readonly DataProtectionRootKey rootKey = DataProtectionRootKeys.Random();

    [Fact]
    public void EncryptProducesTheAesGcmElementThatRoundTrips()
    {
        var plaintext = new XElement("secret", new XAttribute("id", "key-1"), "value");

        var encrypted = new AesGcmXmlEncryptor(new AesGcmKeyRingProtection(rootKey)).Encrypt(
            plaintext
        );
        var element = encrypted.EncryptedElement;
        var decrypted = Decryptor(rootKey).Decrypt(element);

        XNode.DeepEquals(decrypted, plaintext).Should().BeTrue();
        encrypted.DecryptorType.Should().Be<AesGcmXmlDecryptor>();
        element.Name.Should().Be(Namespace + "encryptedSecret");
        ((string?)element.Attribute("version")).Should().Be("1");
        element
            .Elements()
            .Select(child => child.Name.LocalName)
            .Should()
            .Equal("nonce", "tag", "ciphertext");
        element.ToString().Should().NotContain("value");

        Field(element, "nonce").Should().HaveCount(NonceSize);
        Field(element, "tag").Should().HaveCount(TagSize);
        Field(element, "ciphertext")
            .Should()
            .HaveCount(
                plaintext.ToString(SaveOptions.DisableFormatting).Length,
                "AES-GCM ciphertext is as long as its plaintext"
            );
    }

    [Fact]
    public void EncryptSameElementTwiceUsesAFreshNonce()
    {
        var encryptor = new AesGcmXmlEncryptor(new AesGcmKeyRingProtection(rootKey));

        var first = encryptor.Encrypt(new XElement("secret", "value")).EncryptedElement;
        var second = encryptor.Encrypt(new XElement("secret", "value")).EncryptedElement;

        Field(first, "nonce").Should().NotEqual(Field(second, "nonce"));
        Field(first, "ciphertext").Should().NotEqual(Field(second, "ciphertext"));
    }

    [Fact]
    public void DecryptWithAnotherRootKeyFailsClosed()
    {
        var encrypted = Encrypt(rootKey);

        var act = () => Decryptor(DataProtectionRootKeys.Random()).Decrypt(encrypted);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void DecryptRejectsEveryBitFlipInEveryField()
    {
        var decryptor = Decryptor(rootKey);
        var encrypted = Encrypt(rootKey);
        var accepted = new List<string>();

        foreach (var name in new[] { "nonce", "tag", "ciphertext" })
        {
            var original = Field(encrypted, name);
            for (var offset = 0; offset < original.Length; offset++)
            {
                var tampered = (byte[])original.Clone();
                tampered[offset] ^= 0x01;
                var element = new XElement(encrypted);
                element.Element(Namespace + name)!.Value = Convert.ToBase64String(tampered);

                try
                {
                    decryptor.Decrypt(element);
                    accepted.Add($"{name}[{offset}]");
                }
                catch (CryptographicException)
                {
                    continue;
                }
            }
        }

        accepted.Should().BeEmpty();
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("ciphertext")]
    public void DecryptRejectsTruncatedMissingOrMalformedFields(string name)
    {
        var decryptor = Decryptor(rootKey);
        var encrypted = Encrypt(rootKey);

        var shortened = new XElement(encrypted);
        shortened.Element(Namespace + name)!.Value = Convert.ToBase64String(
            Field(encrypted, name).AsSpan(1).ToArray()
        );
        var missing = new XElement(encrypted);
        missing.Element(Namespace + name)!.Remove();
        var notBase64 = new XElement(encrypted);
        notBase64.Element(Namespace + name)!.Value = "not base64";

        decryptor.Invoking(d => d.Decrypt(shortened)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(missing)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(notBase64)).Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptRejectsCiphertextOutsideTheAcceptedSize()
    {
        var decryptor = Decryptor(rootKey);
        var empty = BuildElement(ciphertext: []);
        var oversized = BuildElement(ciphertext: new byte[(1024 * 1024) + 1]);

        decryptor.Invoking(d => d.Decrypt(empty)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(oversized)).Should().Throw<CryptographicException>();
    }

    [Fact]
    public void DecryptRejectsForeignNamespaceAndVersion()
    {
        var decryptor = Decryptor(rootKey);
        var encrypted = Encrypt(rootKey);

        var wrongVersion = new XElement(encrypted);
        wrongVersion.SetAttributeValue("version", 2);
        var missingVersion = new XElement(encrypted);
        missingVersion.Attribute("version")!.Remove();
        var wrongNamespace = new XElement(
            XNamespace.Get("urn:example:foreign") + "encryptedSecret",
            new XAttribute("version", ElementVersion),
            encrypted.Elements().Select(child => new XElement(child))
        );
        var wrongName = new XElement(encrypted);
        wrongName.Name = Namespace + "encryptedKey";

        decryptor.Invoking(d => d.Decrypt(wrongVersion)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(missingVersion)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(wrongNamespace)).Should().Throw<CryptographicException>();
        decryptor.Invoking(d => d.Decrypt(wrongName)).Should().Throw<CryptographicException>();
        FluentActions
            .Invoking(() => decryptor.Decrypt(null!))
            .Should()
            .Throw<ArgumentNullException>();
    }

    [Fact]
    public void DecryptRejectsPlaintextThatIsNotXml()
    {
        var element = EncryptBytes(rootKey, "not xml at all"u8.ToArray());

        var act = () => Decryptor(rootKey).Decrypt(element);

        act.Should()
            .Throw<CryptographicException>()
            .WithMessage("*decrypted Data Protection key is invalid*");
    }

    private static AesGcmXmlDecryptor Decryptor(DataProtectionRootKey key)
    {
        return new AesGcmXmlDecryptor(
            new ServiceCollection()
                .AddSingleton(new AesGcmKeyRingProtection(key))
                .BuildServiceProvider()
        );
    }

    private static XElement Encrypt(DataProtectionRootKey key)
    {
        return new AesGcmXmlEncryptor(new AesGcmKeyRingProtection(key))
            .Encrypt(new XElement("secret", "value"))
            .EncryptedElement;
    }

    private static byte[] Field(XElement element, string name)
    {
        return Convert.FromBase64String(element.Element(Namespace + name)!.Value);
    }

    private static XElement BuildElement(
        byte[]? nonce = null,
        byte[]? tag = null,
        byte[]? ciphertext = null
    )
    {
        return new XElement(
            Namespace + "encryptedSecret",
            new XAttribute("version", ElementVersion),
            new XElement(Namespace + "nonce", Convert.ToBase64String(nonce ?? new byte[NonceSize])),
            new XElement(Namespace + "tag", Convert.ToBase64String(tag ?? new byte[TagSize])),
            new XElement(
                Namespace + "ciphertext",
                Convert.ToBase64String(ciphertext ?? new byte[32])
            )
        );
    }

    private static XElement EncryptBytes(DataProtectionRootKey key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];
        using var cipher = new AesGcm(
            key.DeriveKey(AesGcmKeyRingProtection.KeyPurpose, 32),
            TagSize
        );
        cipher.Encrypt(
            nonce,
            plaintext,
            ciphertext,
            tag,
            "CodigoActivo Data Protection key element v1"u8.ToArray()
        );

        return BuildElement(nonce, tag, ciphertext);
    }
}
