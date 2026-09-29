using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Resources;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class ResourceTests
{
    private const string Body =
        "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Hola\"}]}]}";

    private const string EmptyBody = "{\"type\":\"doc\",\"content\":[]}";

    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    public static TheoryData<bool, string?, string?, ErrorCode> InvalidContents =>
        new()
        {
            { true, Body, "https://a.test", ErrorCode.ResourceDescriptionNotAllowed },
            { true, null, "   ", ErrorCode.ResourceUrlRequired },
            { false, Body, "https://a.test", ErrorCode.ResourceUrlNotAllowed },
            { false, EmptyBody, null, ErrorCode.ResourceDescriptionRequired },
            { false, null, null, ErrorCode.ResourceDescriptionRequired },
        };

    [Theory]
    [MemberData(nameof(InvalidContents))]
    public void ForContentNotFittingTheTypeReturnsValidationError(
        bool isExternal,
        string? description,
        string? url,
        ErrorCode expected
    )
    {
        var content = ResourceContent.For(isExternal, description, url);

        content.IsFailure.Should().BeTrue();
        content.Error!.Kind.Should().Be(ErrorKind.Validation);
        content.Error.Code.Should().Be(expected);
    }

    [Fact]
    public void ForExternalLinkKeepsTrimmedUrlAndEmptyBody()
    {
        var content = ResourceContent.For(true, EmptyBody, "  https://a.test  ");

        content.Value.Url.Should().Be("https://a.test");
        content.Value.Description.Should().Be("{}");
    }

    [Fact]
    public void ForHostedBodyKeepsBodyAndNoUrl()
    {
        var content = ResourceContent.For(false, Body, "  ");

        content.Value.Description.Should().Be(Body);
        content.Value.Url.Should().BeNull();
    }

    [Fact]
    public void CreateThenUpdateAppliesDetailsContentAndAudit()
    {
        var authorId = Guid.NewGuid();
        var resource = Resource.Create(
            new ResourceDetails(
                " Guía ",
                " Intro ",
                SeedIds.ResourceTypes.Internal,
                Guid.NewGuid()
            ),
            ResourceContent.For(false, Body, null).Value,
            authorId,
            Now
        );

        resource.Title.Should().Be("Guía");
        resource.Subtitle.Should().Be("Intro");
        resource.Description.Should().Be(Body);
        resource.Url.Should().BeNull();
        resource.CreatedBy.Should().Be(authorId);
        resource.CreatedAt.Should().Be(Now);

        var editorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        resource.Update(
            new ResourceDetails("Enlace", "Externo", SeedIds.ResourceTypes.External, thumbnailId),
            ResourceContent.For(true, null, "https://b.test").Value,
            editorId,
            Now.AddDays(2)
        );

        resource.Title.Should().Be("Enlace");
        resource.ResourceTypeId.Should().Be(SeedIds.ResourceTypes.External);
        resource.ThumbnailId.Should().Be(thumbnailId);
        resource.Description.Should().Be("{}");
        resource.Url.Should().Be("https://b.test");
        resource.UpdatedBy.Should().Be(editorId);
        resource.UpdatedAt.Should().Be(Now.AddDays(2));
    }
}
