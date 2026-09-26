using System.Text;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class DisposableEmailDomainListTests
{
    private static DisposableEmailDomainListResult Parse(string text)
    {
        return DisposableEmailDomainList.Parse(Encoding.UTF8.GetBytes(text));
    }

    [Fact]
    public void ParseGenuineListReturnsEveryNormalizedDomainOnce()
    {
        var text =
            $"{(char)0xFEFF}# disposable domains\r\n"
            + DisposableEmailDomainLists.Genuine(
                "",
                "   ",
                "  # a comment  ",
                "  Mailinator.COM  ",
                "mailinator.com.",
                "inbox.example-mail.co.uk",
                "münchen.de",
                "xn--9kq967o.com"
            );

        var result = Parse(text);

        result.Rejection.Should().BeNull();
        result
            .Domains.Should()
            .HaveCount(DisposableEmailDomainList.MinDomains + 4)
            .And.Contain([
                "disposable0.test",
                "mailinator.com",
                "inbox.example-mail.co.uk",
                "xn--mnchen-3ya.de",
                "xn--9kq967o.com",
            ]);
    }

    [Fact]
    public void ParseListOfLookalikesOfProtectedProvidersIsAccepted()
    {
        var result = Parse(
            DisposableEmailDomainLists.Genuine(
                "gmail.or.at",
                "outlook.edu.pl",
                "mail.gw",
                "gmailx.com"
            )
        );

        result.Rejection.Should().BeNull();
        result.Domains.Should().Contain(["gmail.or.at", "outlook.edu.pl", "mail.gw", "gmailx.com"]);
    }

    [Fact]
    public void ParseBodyLargerThanTheMaximumIsRejected()
    {
        var content = new byte[DisposableEmailDomainList.MaxBytes + 1];
        Array.Fill(content, (byte)'a');

        DisposableEmailDomainList
            .Parse(content)
            .Rejection.Should()
            .Be(DisposableEmailDomainListRejection.TooLarge);
    }

    [Fact]
    public void ParseBodyThatIsNotUtf8IsRejected()
    {
        byte[] content =
        [
            .. Encoding.UTF8.GetBytes(DisposableEmailDomainLists.Genuine()),
            0xC3,
            0x28,
        ];

        var result = DisposableEmailDomainList.Parse(content);

        result.Rejection.Should().Be(DisposableEmailDomainListRejection.NotText);
        result.Domains.Should().BeEmpty();
    }

    [Theory]
    [InlineData("<!DOCTYPE html>")]
    [InlineData("{\"domains\":[\"mailinator.com\"]}")]
    [InlineData("mailinator.com,guerrillamail.com")]
    [InlineData("not a domain.com")]
    [InlineData("localhost")]
    [InlineData("-mailinator.com")]
    [InlineData("mailinator-.com")]
    [InlineData("mail_inator.com")]
    [InlineData("mailinator..com")]
    [InlineData("*.mailinator.com")]
    [InlineData("@mailinator.com")]
    public void ParseLineThatIsNotADomainNameRejectsTheWholeList(string line)
    {
        var result = Parse(DisposableEmailDomainLists.Genuine(line));

        result.Rejection.Should().Be(DisposableEmailDomainListRejection.MalformedEntry);
        result.Domains.Should().BeEmpty();
    }

    [Fact]
    public void ParseLabelOrNameBeyondTheDnsLimitsRejectsTheWholeList()
    {
        var longLabel = $"{new string('a', 64)}.com";
        var longName = string.Join('.', Enumerable.Repeat(new string('b', 50), 5)) + ".com";

        Parse(DisposableEmailDomainLists.Genuine(longLabel))
            .Rejection.Should()
            .Be(DisposableEmailDomainListRejection.MalformedEntry);
        Parse(DisposableEmailDomainLists.Genuine(longName))
            .Rejection.Should()
            .Be(DisposableEmailDomainListRejection.MalformedEntry);
    }

    [Theory]
    [InlineData("")]
    [InlineData("mailinator.com\nguerrillamail.com")]
    public void ParseShortListIsRejected(string text)
    {
        Parse(text).Rejection.Should().Be(DisposableEmailDomainListRejection.TooFewDomains);
    }

    [Fact]
    public void ParseOneDomainShortOfTheMinimumIsRejected()
    {
        var text = string.Join(
            '\n',
            DisposableEmailDomainLists.Domains(DisposableEmailDomainList.MinDomains - 1)
        );

        Parse(text).Rejection.Should().Be(DisposableEmailDomainListRejection.TooFewDomains);
    }

    [Theory]
    [InlineData("gmail.com")]
    [InlineData("HOTMAIL.ES")]
    [InlineData("outlook.com.")]
    [InlineData("telefonica.net")]
    public void ParseListThatWouldRefuseAProtectedProviderIsRejected(string line)
    {
        var result = Parse(DisposableEmailDomainLists.Genuine(line));

        result.Rejection.Should().Be(DisposableEmailDomainListRejection.ProtectedDomainListed);
        result.Domains.Should().BeEmpty();
    }

    [Fact]
    public void ProtectedDomainsAreValidNormalizedDomainNames()
    {
        var result = Parse(
            DisposableEmailDomainLists.Genuine([.. DisposableEmailDomainList.ProtectedDomains])
        );

        result.Rejection.Should().Be(DisposableEmailDomainListRejection.ProtectedDomainListed);
        DisposableEmailDomainList
            .ProtectedDomains.Should()
            .OnlyContain(domain =>
                string.Equals(domain, domain.ToLowerInvariant(), StringComparison.Ordinal)
                && domain.Contains('.', StringComparison.Ordinal)
            );
    }
}
