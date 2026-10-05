using AwesomeAssertions;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("600111222")]
    [InlineData("+34 600 111 222")]
    [InlineData("(+34) 600-111-222")]
    [InlineData("987.654.321")]
    [InlineData("5550100")]
    [InlineData("+123456789012345")]
    public void IsValidWellFormedPhonesReturnsTrue(string phone)
    {
        PhoneNumber.IsValid(phone).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("555010")]
    [InlineData("abc")]
    [InlineData("600111222x")]
    [InlineData("600+111222")]
    [InlineData("++34600111222")]
    [InlineData("+1234567890123456")]
    public void IsValidMalformedPhonesReturnsFalse(string phone)
    {
        PhoneNumber.IsValid(phone).Should().BeFalse();
    }
}
