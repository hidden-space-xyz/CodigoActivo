using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class ResourceTests
{
    private const string Body =
        "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Hola\"}]}]}";

    private const string EmptyBody = "{\"type\":\"doc\",\"content\":[]}";

    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    public static TheoryData<ResourceType, string?, string?, DomainErrorCode> InvalidContents =>
        new()
        {
            {
                ResourceType.External,
                Body,
                "https://a.test",
                DomainErrorCode.ResourceDescriptionNotAllowed
            },
            { ResourceType.External, null, "   ", DomainErrorCode.ResourceUrlRequired },
            {
                ResourceType.Internal,
                Body,
                "https://a.test",
                DomainErrorCode.ResourceUrlNotAllowed
            },
            { ResourceType.Internal, EmptyBody, null, DomainErrorCode.ResourceDescriptionRequired },
            { ResourceType.Internal, null, null, DomainErrorCode.ResourceDescriptionRequired },
        };

    [Theory]
    [MemberData(nameof(InvalidContents))]
    public void ForContentNotFittingTheTypeReturnsValidationError(
        ResourceType type,
        string? description,
        string? url,
        DomainErrorCode expected
    )
    {
        var content = ResourceContent.For(type, RichText.FromOptional(description), url);

        content.IsFailure.Should().BeTrue();
        content.Error!.Kind.Should().Be(ErrorKind.Validation);
        content.Error.Code.Should().Be(expected);
    }

    [Fact]
    public void ForExternalLinkKeepsTrimmedUrlAndEmptyBody()
    {
        var content = ResourceContent.For(
            ResourceType.External,
            RichText.From(EmptyBody),
            "  https://a.test  "
        );

        content.Value.Url.Should().Be("https://a.test");
        content.Value.Description!.Json.Should().Be("{}");
    }

    [Fact]
    public void ForHostedBodyKeepsBodyAndNoUrl()
    {
        var content = ResourceContent.For(ResourceType.Internal, RichText.From(Body), "  ");

        content.Value.Description!.Json.Should().Be(Body);
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
                ResourceType.Internal,
                StoredFileId.From(Guid.NewGuid())
            ),
            ResourceContent.For(ResourceType.Internal, RichText.From(Body), null).Value,
            UserId.From(authorId),
            Now
        );

        resource.Title.Should().Be("Guía");
        resource.Subtitle.Should().Be("Intro");
        resource.Description!.Json.Should().Be(Body);
        resource.Url.Should().BeNull();
        resource.CreatedBy.Value.Should().Be(authorId);
        resource.CreatedAt.Should().Be(Now);

        var editorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        resource.Update(
            new ResourceDetails(
                "Enlace",
                "Externo",
                ResourceType.External,
                StoredFileId.From(thumbnailId)
            ),
            ResourceContent.For(ResourceType.External, RichText.Empty, "https://b.test").Value,
            UserId.From(editorId),
            Now.AddDays(2)
        );

        resource.Title.Should().Be("Enlace");
        resource.ResourceType.Should().Be(ResourceType.External);
        resource.ThumbnailId.Value.Should().Be(thumbnailId);
        resource.Description!.Json.Should().Be("{}");
        resource.Url.Should().Be("https://b.test");
        resource.UpdatedBy.Should().Be(UserId.From(editorId));
        resource.UpdatedAt.Should().Be(Now.AddDays(2));
    }
}
