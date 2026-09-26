using AwesomeAssertions;
using CodigoActivo.Domain.Communication;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class EmailDomainsTests
{
    [Theory]
    [InlineData("Mailinator.COM", "mailinator.com")]
    [InlineData("  mailinator.com.  ", "mailinator.com")]
    [InlineData("mailinator.com...", "mailinator.com")]
    [InlineData("münchen.de", "xn--mnchen-3ya.de")]
    [InlineData("XN--MNCHEN-3YA.DE", "xn--mnchen-3ya.de")]
    public void NormalizeDomainNameReturnsLowercaseAscii(string domain, string expected)
    {
        EmailDomains.Normalize(domain).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" . ")]
    public void NormalizeNothingLeftReturnsNull(string? domain)
    {
        EmailDomains.Normalize(domain).Should().BeNull();
    }

    [Fact]
    public void NormalizeInvalidInternationalizedNameOnlyLowercases()
    {
        var label = new string('a', 64);

        EmailDomains.Normalize($"{label.ToUpperInvariant()}.COM").Should().Be($"{label}.com");
    }

    [Fact]
    public void SelfAndParentsSubdomainStopsBeforeTheTopLevelDomain()
    {
        EmailDomains
            .SelfAndParents("a.b.mailinator.com")
            .Should()
            .Equal("a.b.mailinator.com", "b.mailinator.com", "mailinator.com");
    }

    [Theory]
    [InlineData("com")]
    [InlineData("mailinator..com")]
    [InlineData(".com")]
    public void SelfAndParentsWithoutTwoNonEmptyLabelsReturnsEmpty(string domain)
    {
        EmailDomains.SelfAndParents(domain).Should().BeEmpty();
    }

    [Fact]
    public void LookupNamesAddressUsesTheNormalizedDomainAfterTheLastAt()
    {
        EmailDomains
            .LookupNames("\"odd@local\"@Inbox.Mailinator.com.")
            .Should()
            .Equal("inbox.mailinator.com", "mailinator.com");
    }

    [Theory]
    [InlineData("no-at-sign.example.com")]
    [InlineData("user@")]
    [InlineData("user@localhost")]
    public void LookupNamesWithoutUsableDomainReturnsEmpty(string email)
    {
        EmailDomains.LookupNames(email).Should().BeEmpty();
    }
}
