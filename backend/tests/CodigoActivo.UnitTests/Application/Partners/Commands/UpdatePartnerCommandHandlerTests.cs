using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Commands;

public sealed class UpdatePartnerCommandHandlerTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly UpdatePartnerCommandHandler sut;

    public UpdatePartnerCommandHandlerTests()
    {
        sut = new UpdatePartnerCommandHandler(partners, files, currentUser, clock);
    }

    private static UpdatePartnerCommand Command(
        PartnerId partnerId,
        string name = "Acme",
        int tier = 1,
        string? website = null,
        StoredFileId? thumbnailId = null
    )
    {
        return new UpdatePartnerCommand(
            partnerId,
            name,
            new DateOnly(2025, 2, 2),
            tier,
            website,
            thumbnailId ?? StoredFileId.New()
        );
    }

    [Fact]
    public async Task HandleAsyncPartnerMissingReturnsNotFound()
    {
        partners.Finds(null);

        var result = await sut.HandleAsync(
            Command(PartnerId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.PartnerNotFound);
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequestAndKeepsThePartner()
    {
        var partner = NewPartner("Old");
        partner.PullDomainEvents();
        partners.Finds(partner);
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(
            Command(partner.Id, "New"),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.PartnerThumbnailNotFound);
        partner.Name.Should().Be("Old");
        partner.PullDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncValidCommandReplacesProfileByTheCurrentUser()
    {
        var partner = NewPartner("Old", tier: 1);
        partners.Finds(partner);
        files.ThumbnailExists(true);
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            Command(partner.Id, "  New  ", 5, "https://new.test"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        partner.Name.Should().Be("New");
        partner.Tier.Should().Be(5);
        partner.UpdatedBy.Should().Be(currentUser.Id);
        partner.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncThumbnailReplacedRaisesUpdateReleasingThePreviousFile()
    {
        var partner = NewPartner();
        var previousThumbnailId = partner.ThumbnailId;
        partner.PullDomainEvents();
        partners.Finds(partner);
        files.ThumbnailExists(true);
        var thumbnailId = StoredFileId.New();

        var result = await sut.HandleAsync(
            Command(partner.Id, thumbnailId: thumbnailId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var updated = partner
            .PullDomainEvents()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<PartnerUpdated>()
            .Subject;
        updated.Should().Be(new PartnerUpdated(partner.Id, previousThumbnailId, thumbnailId));
        updated.ReleasedFileIds.Should().Equal(previousThumbnailId);
    }

    [Fact]
    public async Task HandleAsyncThumbnailUnchangedReleasesNoFile()
    {
        var partner = NewPartner();
        partner.PullDomainEvents();
        partners.Finds(partner);
        files.ThumbnailExists(true);

        var result = await sut.HandleAsync(
            Command(partner.Id, thumbnailId: partner.ThumbnailId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        partner
            .PullDomainEvents()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<PartnerUpdated>()
            .Which.ReleasedFileIds.Should()
            .BeEmpty();
    }
}
