using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Users;

public sealed class EmailAddressTests
{
    [Theory]
    [InlineData("ana@example.com", "ana@example.com")]
    [InlineData("  Ana.Garcia+club@Example.COM ", "ana.garcia+club@example.com")]
    [InlineData("o'neil@sub.example.es", "o'neil@sub.example.es")]
    public void CreateWellFormedEmailReturnsNormalizedValue(string email, string expected)
    {
        var result = EmailAddress.Create(email);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("@example.com")]
    [InlineData("ana@")]
    [InlineData("ana@b@example.com")]
    [InlineData("ana garcia@example.com")]
    [InlineData("ana,bea@example.com")]
    [InlineData("ana;bea@example.com")]
    [InlineData("Ana <ana@example.com>")]
    [InlineData("\"ana\"@example.com")]
    [InlineData("ana@[127.0.0.1]")]
    [InlineData("ana(x)@example.com")]
    [InlineData("ana\\@example.com")]
    [InlineData("ana:x@example.com")]
    [InlineData("ana\r\n@example.com")]
    [InlineData("ana\u0000@example.com")]
    [InlineData("ana\t@example.com")]
    public void CreateMalformedEmailReturnsUserEmailInvalid(string? email)
    {
        var result = EmailAddress.Create(email);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(DomainErrorCode.UserEmailInvalid);
    }
}
