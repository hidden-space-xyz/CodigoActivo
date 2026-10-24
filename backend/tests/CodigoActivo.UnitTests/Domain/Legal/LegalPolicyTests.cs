using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Legal;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Legal;

public sealed class LegalPolicyTests
{
    private static readonly DateTimeOffset Monday = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Friday = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private static RichText Text(string text)
    {
        return RichText.From(
            $$"""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"{{text}}"}]}]}"""
        );
    }

    [Fact]
    public void PublishWithTextStartsAtTheFirstVersion()
    {
        var policy = LegalPolicy.Publish(LegalPolicyKind.Privacy, Text("Primera"), Monday).Value;

        policy.Id.Value.Should().NotBeEmpty();
        policy.Kind.Should().Be(LegalPolicyKind.Privacy);
        policy.Version.Should().Be(LegalPolicy.FirstVersion);
        policy.Content.Should().Be(Text("Primera"));
        policy.PublishedAt.Should().Be(Monday);
        policy.PreviousVersions.Should().BeEmpty();
        policy.PullDomainEvents().Should().Equal(new LegalPolicyPublished(policy.Id));
    }

    [Fact]
    public void PublishWithoutTextIsRefused()
    {
        LegalPolicy
            .Publish(LegalPolicyKind.Cookies, Text("  "), Monday)
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.LegalPolicyContentRequired);
    }

    [Fact]
    public void ReviseNewContentKeepsTheReplacedVersion()
    {
        var policy = LegalPolicy.Publish(LegalPolicyKind.Cookies, Text("Primera"), Monday).Value;
        policy.PullDomainEvents();

        policy.Revise(Text("Segunda"), Friday).IsSuccess.Should().BeTrue();

        policy.PullDomainEvents().Should().Equal(new LegalPolicyPublished(policy.Id));
        policy.Version.Should().Be(2);
        policy.Content.Should().Be(Text("Segunda"));
        policy.PublishedAt.Should().Be(Friday);
        policy
            .PreviousVersions.Select(previous =>
                (previous.LegalPolicyId, previous.Number, previous.Content, previous.PublishedAt)
            )
            .Should()
            .Equal((policy.Id, LegalPolicy.FirstVersion, Text("Primera"), Monday));
    }

    [Fact]
    public void ReviseSameContentLaidOutDifferentlyChangesNothing()
    {
        var policy = LegalPolicy.Publish(LegalPolicyKind.Privacy, Text("Igual"), Monday).Value;
        policy.PullDomainEvents();

        policy
            .Revise(
                RichText.From(
                    """{ "content": [{ "content": [{ "text": "Igual", "type": "text" }], "type": "paragraph" }], "type": "doc" }"""
                ),
                Friday
            )
            .IsSuccess.Should()
            .BeTrue();

        policy.Version.Should().Be(LegalPolicy.FirstVersion);
        policy.PublishedAt.Should().Be(Monday);
        policy.PreviousVersions.Should().BeEmpty();
        policy.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void ReviseWithoutTextIsRefusedAndKeepsTheVersionInForce()
    {
        var policy = LegalPolicy.Publish(LegalPolicyKind.Privacy, Text("Vigente"), Monday).Value;
        policy.PullDomainEvents();

        policy
            .Revise(RichText.Empty, Friday)
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.LegalPolicyContentRequired);

        policy.PullDomainEvents().Should().BeEmpty();
        policy.Version.Should().Be(LegalPolicy.FirstVersion);
        policy.Content.Should().Be(Text("Vigente"));
        policy.PreviousVersions.Should().BeEmpty();
    }
}
