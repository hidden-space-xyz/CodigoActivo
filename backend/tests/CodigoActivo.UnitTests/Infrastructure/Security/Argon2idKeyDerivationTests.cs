using System.Globalization;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Security;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Security;

public sealed class Argon2idKeyDerivationTests
{
    private const string Password = "a-strong-data-protection-certificate-password";

    [Fact]
    public void DeriveKeyReturnsThirtyTwoDeterministicBytes()
    {
        var salt = Argon2idKeyDerivation.CreateSalt();

        var key = Argon2idKeyDerivation.DeriveKey(Password, salt);
        var again = Argon2idKeyDerivation.DeriveKey(Password, salt);

        key.Should().HaveCount(32);
        key.Should().Equal(again);
        key.Should().NotEqual(new byte[32]);
    }

    [Fact]
    public void DeriveKeyWithDifferentSaltOrPasswordReturnsDifferentKeys()
    {
        var salt = Argon2idKeyDerivation.CreateSalt();
        var otherSalt = Argon2idKeyDerivation.CreateSalt();

        var key = Argon2idKeyDerivation.DeriveKey(Password, salt);

        key.Should().NotEqual(Argon2idKeyDerivation.DeriveKey(Password, otherSalt));
        key.Should().NotEqual(Argon2idKeyDerivation.DeriveKey(Password + "!", salt));
    }

    [Fact]
    public void CreateSaltReturnsDistinctSaltsOfTheDeclaredSize()
    {
        var salt = Argon2idKeyDerivation.CreateSalt();
        var other = Argon2idKeyDerivation.CreateSalt();

        salt.Should().HaveCount(Argon2idKeyDerivation.SaltSize);
        Argon2idKeyDerivation.SaltSize.Should().Be(16);
        salt.Should().NotEqual(other);
    }

    [Fact]
    public void DeriveKeyUsesTheSameCostParametersAsThePasswordHasher()
    {
        var parts = new Argon2idPasswordHasher().Hash(Password).Split('$');

        parts[1]
            .Should()
            .Be(Argon2idKeyDerivation.Iterations.ToString(CultureInfo.InvariantCulture));
        parts[2]
            .Should()
            .Be(Argon2idKeyDerivation.MemoryKiB.ToString(CultureInfo.InvariantCulture));
        parts[3]
            .Should()
            .Be(Argon2idKeyDerivation.Parallelism.ToString(CultureInfo.InvariantCulture));
        Convert.FromBase64String(parts[4]).Should().HaveCount(Argon2idKeyDerivation.SaltSize);
        Convert.FromBase64String(parts[5]).Should().HaveCount(Argon2idKeyDerivation.KeySize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    [InlineData(32)]
    public void DeriveKeyWithUnexpectedSaltLengthThrows(int saltSize)
    {
        var act = () => Argon2idKeyDerivation.DeriveKey(Password, new byte[saltSize]);

        act.Should().Throw<ArgumentException>().WithParameterName("salt");
    }

    [Fact]
    public void DeriveKeyWithoutPasswordThrows()
    {
        var salt = Argon2idKeyDerivation.CreateSalt();

        FluentActions
            .Invoking(() => Argon2idKeyDerivation.DeriveKey(null!, salt))
            .Should()
            .Throw<ArgumentNullException>();
        FluentActions
            .Invoking(() => Argon2idKeyDerivation.DeriveKey(string.Empty, salt))
            .Should()
            .Throw<ArgumentException>();
    }
}
