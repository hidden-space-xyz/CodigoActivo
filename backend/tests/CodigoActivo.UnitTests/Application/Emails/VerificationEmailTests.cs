using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class VerificationEmailTests
{
    private const string VerifyUrl = "https://app.test/verify-account?userId=abc&code=123456";

    [Fact]
    public void Create_addresses_the_recipient_and_contains_the_code_link_and_lifetime()
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
        // The HTML body HTML-encodes the URL (& -> &amp;), so assert on its stable fragments.
        message
            .HtmlBody.Should()
            .Contain("Ana")
            .And.Contain("123456")
            .And.Contain("verify-account?userId=abc")
            .And.Contain("code=123456")
            .And.Contain("15 minutos");
    }

    [Fact]
    public void Create_html_encodes_the_recipient_name()
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
