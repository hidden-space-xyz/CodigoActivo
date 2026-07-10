using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class PartnerServiceTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IFileService fileService = Substitute.For<IFileService>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly PartnerService sut;

    public PartnerServiceTests()
    {
        sut = new PartnerService(partners, files, fileService, new FakeQueryExecutor(), clock, uow);
    }

    private void HasPartners(params Partner[] items) =>
        partners.Query().Returns(items.AsQueryable());

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(
                Arg.Any<Expression<Func<FileEntity, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);

    private static Partner NewPartner(
        string name = "Acme",
        int tier = 1,
        string? web = "https://acme.test"
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tier = tier,
            Web = web,
            FromDate = new DateOnly(2024, 1, 1),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };

    [Fact]
    public async Task ListAsync_TierFilter_ReturnsMatchingTier()
    {
        HasPartners(NewPartner("Gold", tier: 1), NewPartner("Silver", tier: 2));

        var result = await sut.ListAsync(
            new PartnerListQuery { Tier = 2 },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Silver");
    }

    [Fact]
    public async Task ListAsync_NameSearch_IsAccentAndCaseInsensitive()
    {
        HasPartners(NewPartner("Fundación Ávila"), NewPartner("Banco"));

        var result = await sut.ListAsync(
            new PartnerListQuery { Name = "avila" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Fundación Ávila");
    }

    [Fact]
    public async Task ListAsync_WebsiteSearch_MatchesSubstring()
    {
        HasPartners(
            NewPartner("A", web: "https://alpha.org"),
            NewPartner("B", web: "https://beta.org")
        );

        var result = await sut.ListAsync(
            new PartnerListQuery { Website = "beta" },
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Website.Should().Be("https://beta.org");
    }

    [Fact]
    public async Task ListAsync_ExplicitDescendingSort_OrdersDescending()
    {
        HasPartners(NewPartner("Acme"), NewPartner("Zeta"), NewPartner("Mint"));

        var result = await sut.ListAsync(
            new PartnerListQuery { Sort = "-name" },
            TestContext.Current.CancellationToken
        );

        result.Items.Select(p => p.Name).Should().ContainInOrder("Zeta", "Mint", "Acme");
    }

    [Fact]
    public async Task ListAsync_NoSortSpecified_OrdersByTierAscendingThenFromDateDescending()
    {
        var tier1Newer = NewPartner("Tier1Newer", tier: 1);
        tier1Newer.FromDate = new DateOnly(2023, 6, 1);
        var tier1Older = NewPartner("Tier1Older", tier: 1);
        tier1Older.FromDate = new DateOnly(2020, 6, 1);
        var tier2 = NewPartner("Tier2", tier: 2);
        tier2.FromDate = new DateOnly(2025, 1, 1);
        var tier3 = NewPartner("Tier3", tier: 3);
        tier3.FromDate = new DateOnly(2019, 1, 1);
        HasPartners(tier2, tier3, tier1Older, tier1Newer);

        var result = await sut.ListAsync(
            new PartnerListQuery(),
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(p => p.Name)
            .Should()
            .ContainInOrder("Tier1Newer", "Tier1Older", "Tier2", "Tier3");
    }

    [Fact]
    public async Task GetByIdAsync_PartnerExists_ReturnsPartner()
    {
        var partner = NewPartner();
        HasPartners(partner);

        var result = await sut.GetByIdAsync(partner.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(partner.Id);
    }

    [Fact]
    public async Task GetByIdAsync_PartnerMissing_ReturnsNotFound()
    {
        HasPartners();

        var result = await sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.PartnerNotFound);
    }

    [Fact]
    public async Task CreateAsync_ThumbnailMissing_ReturnsBadRequestAndDoesNotPersist()
    {
        ThumbnailExists(false);
        var request = new CreatePartnerRequest(
            "  Acme  ",
            new DateOnly(2024, 1, 1),
            1,
            " https://acme.test ",
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        await partners
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsTrimmedNormalizedPartner()
    {
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var request = new CreatePartnerRequest(
            "  Acme  ",
            new DateOnly(2024, 3, 4),
            2,
            " https://acme.test ",
            thumbnailId
        );

        var result = await sut.CreateAsync(request, caller, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Acme");
        result.Value.Website.Should().Be("https://acme.test");
        result.Value.Tier.Should().Be(2);
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        await partners
            .Received(1)
            .AddAsync(
                Arg.Is<Partner>(p =>
                    p.Name == "Acme" && p.Web == "https://acme.test" && p.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_BlankWebsite_StoresNullWebsite()
    {
        ThumbnailExists(true);
        var request = new CreatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            0,
            "   ",
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Value.Website.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PartnerMissing_ReturnsNotFound()
    {
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Partner?)null);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.PartnerNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailMissing_ReturnsBadRequest()
    {
        var partner = NewPartner();
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(partner);
        ThumbnailExists(false);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            partner.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_MutatesAndPersists()
    {
        var partner = NewPartner("Old", tier: 1);
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(partner);
        ThumbnailExists(true);
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdatePartnerRequest(
            "  New  ",
            new DateOnly(2025, 2, 2),
            5,
            "https://new.test",
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            partner.Id,
            request,
            caller,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        partner.Name.Should().Be("New");
        partner.Tier.Should().Be(5);
        partner.UpdatedBy.Should().Be(caller);
        partner.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailReplaced_CleansUpPreviousFileAfterSave()
    {
        var partner = NewPartner();
        var previousThumbnailId = partner.ThumbnailId;
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(partner);
        ThumbnailExists(true);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            partner.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ThumbnailUnchanged_DoesNotCleanUp()
    {
        var partner = NewPartner();
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(partner);
        ThumbnailExists(true);
        var request = new UpdatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            1,
            null,
            partner.ThumbnailId
        );

        var result = await sut.UpdateAsync(
            partner.Id,
            request,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_PartnerMissing_ReturnsNotFound()
    {
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Partner?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be(ErrorCode.PartnerNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await fileService
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(default, TestContext.Current.CancellationToken);
    }
}
