using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class VerificationEmailTests
{
    private const string VerifyUrl = "https://app.test/verify-account?userId=abc&code=123456";

    [Fact]
    public void Create_ValidRequest_AddressesRecipientWithCodeLinkAndLifetime()
    {
        var message = VerificationEmail.Create(
            "ana@test.com",
            "Ana",
            "123456",
            VerifyUrl,
            TimeSpan.FromMinutes(15)
        );

        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message
            .Subject.Should()
            .NotContain("123456", "the OTP must not appear in the subject line");
        message
            .TextBody.Should()
            .Contain("Ana")
            .And.Contain("123456")
            .And.Contain(VerifyUrl)
            .And.Contain("15 minutos");
        message
            .HtmlBody.Should()
            .Contain("Ana")
            .And.Contain("123456")
            .And.Contain("verify-account?userId=abc")
            .And.Contain("code=123456")
            .And.Contain("15 minutos");
    }

    [Fact]
    public void Create_ScriptInName_HtmlEncodesRecipientName()
    {
        var message = VerificationEmail.Create(
            "ana@test.com",
            "<script>alert(1)</script>",
            "123456",
            VerifyUrl,
            TimeSpan.FromMinutes(15)
        );

        message.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
    }
}
