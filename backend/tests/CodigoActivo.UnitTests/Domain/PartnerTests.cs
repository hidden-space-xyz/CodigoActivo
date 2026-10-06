using AwesomeAssertions;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class PartnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public void CreateDetailsWithSpacesStoresTrimmedProfileAndAuthor()
    {
        var authorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();

        var partner = Partner.Create(
            new PartnerDetails(
                "  Acme  ",
                new DateOnly(2024, 5, 6),
                2,
                "  https://acme.test ",
                StoredFileId.From(thumbnailId)
            ),
            UserId.From(authorId),
            Now
        );

        partner.Id.Value.Should().NotBeEmpty();
        partner.Name.Should().Be("Acme");
        partner.FromDate.Should().Be(new DateOnly(2024, 5, 6));
        partner.Tier.Should().Be(2);
        partner.Web.Should().Be("https://acme.test");
        partner.ThumbnailId.Value.Should().Be(thumbnailId);
        partner.CreatedBy.Value.Should().Be(authorId);
        partner.CreatedAt.Should().Be(Now);
        partner.UpdatedAt.Should().BeNull();
        partner.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void CreateBlankWebStoresNoWebsite()
    {
        var partner = Partner.Create(
            new PartnerDetails(
                "Acme",
                new DateOnly(2024, 1, 1),
                1,
                "   ",
                StoredFileId.From(Guid.NewGuid())
            ),
            UserId.From(Guid.NewGuid()),
            Now
        );

        partner.Web.Should().BeNull();
    }

    [Fact]
    public void UpdateNewDetailsReplacesProfileAndRecordsEditor()
    {
        var authorId = Guid.NewGuid();
        var partner = Partner.Create(
            new PartnerDetails(
                "Old",
                new DateOnly(2024, 1, 1),
                1,
                null,
                StoredFileId.From(Guid.NewGuid())
            ),
            UserId.From(authorId),
            Now
        );
        var editorId = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();

        partner.Update(
            new PartnerDetails(
                " New ",
                new DateOnly(2025, 2, 2),
                3,
                "https://new.test",
                StoredFileId.From(thumbnailId)
            ),
            UserId.From(editorId),
            Now.AddDays(1)
        );

        partner.Name.Should().Be("New");
        partner.FromDate.Should().Be(new DateOnly(2025, 2, 2));
        partner.Tier.Should().Be(3);
        partner.Web.Should().Be("https://new.test");
        partner.ThumbnailId.Value.Should().Be(thumbnailId);
        partner.CreatedBy.Value.Should().Be(authorId);
        partner.CreatedAt.Should().Be(Now);
        partner.UpdatedBy.Should().Be(UserId.From(editorId));
        partner.UpdatedAt.Should().Be(Now.AddDays(1));
    }
}
