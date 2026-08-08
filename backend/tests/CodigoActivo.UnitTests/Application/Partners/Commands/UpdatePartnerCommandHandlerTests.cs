using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Commands;

public sealed class UpdatePartnerCommandHandlerTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdatePartnerCommandHandler sut;

    public UpdatePartnerCommandHandlerTests()
    {
        sut = new UpdatePartnerCommandHandler(
            partners,
            files,
            orphanCleaner,
            clock,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsync_PartnerMissing_ReturnsNotFound()
    {
        partners.Finds(null);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdatePartnerCommand(Guid.NewGuid(), request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.PartnerNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ThumbnailMissing_ReturnsBadRequest()
    {
        var partner = NewPartner();
        partners.Finds(partner);
        files.ThumbnailExists(false);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdatePartnerCommand(partner.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_MutatesPersistsAndInvalidatesCache()
    {
        var partner = NewPartner("Old", tier: 1);
        partners.Finds(partner);
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdatePartnerRequest(
            "  New  ",
            new DateOnly(2025, 2, 2),
            5,
            "https://new.test",
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdatePartnerCommand(partner.Id, request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        partner.Name.Should().Be("New");
        partner.Tier.Should().Be(5);
        partner.UpdatedBy.Should().Be(caller);
        partner.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Partners)
                )
            );
    }

    [Fact]
    public async Task HandleAsync_ThumbnailReplaced_CleansUpPreviousFileAfterSave()
    {
        var partner = NewPartner();
        var previousThumbnailId = partner.ThumbnailId;
        partners.Finds(partner);
        files.ThumbnailExists(true);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdatePartnerCommand(partner.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .Received(1)
            .DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ThumbnailUnchanged_DoesNotCleanUp()
    {
        var partner = NewPartner();
        partners.Finds(partner);
        files.ThumbnailExists(true);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            partner.ThumbnailId
        );

        var result = await sut.HandleAsync(
            new UpdatePartnerCommand(partner.Id, request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(Guid.Empty, TestContext.Current.CancellationToken);
    }
}
