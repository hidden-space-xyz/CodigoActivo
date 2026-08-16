using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class VerificationEmailTests
{
    private const string VerifyUrl = "https://app.test/verify-account?userId=abc&code=123456";
    private const string SiteUrl = "https://app.test";

    [Fact]
    public void CreateValidRequestAddressesRecipientWithLinkAndLifetime()
    {
        var message = VerificationEmail.Create(
            "ana@test.com",
            "Ana",
            VerifyUrl,
            SiteUrl,
            TimeSpan.FromMinutes(15)
        );

        message.ToAddress.Should().Be("ana@test.com");
        message.ToName.Should().Be("Ana");
        message
            .Subject.Should()
            .NotContain("123456", "the OTP must not appear in the subject line");
        message.TextBody.Should().Contain("Ana").And.Contain(VerifyUrl).And.Contain("15 minutos");
        message
            .HtmlBody.Should()
            .Contain("Ana")
            .And.Contain("verify-account?userId=abc")
            .And.Contain("code=123456")
            .And.Contain("15 minutos");
    }

    [Fact]
    public void CreateValidRequestShowsTheOtpOnlyInsideTheVerificationLink()
    {
        var message = VerificationEmail.Create(
            "ana@test.com",
            "Ana",
            VerifyUrl,
            SiteUrl,
            TimeSpan.FromMinutes(15)
        );

        message
            .TextBody.Replace(VerifyUrl, string.Empty, StringComparison.Ordinal)
            .Should()
            .NotContain("123456", "the OTP must not appear as copyable text outside the link");
    }

    [Fact]
    public void CreateScriptInNameHtmlEncodesRecipientName()
    {
        var message = VerificationEmail.Create(
            "ana@test.com",
            "<script>alert(1)</script>",
            VerifyUrl,
            SiteUrl,
            TimeSpan.FromMinutes(15)
        );

        message.HtmlBody.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
    }
}
