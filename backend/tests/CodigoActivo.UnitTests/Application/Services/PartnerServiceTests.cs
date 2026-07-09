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
    public async Task ListAsync_projects_and_pages()
    {
        HasPartners(NewPartner("Acme"), NewPartner("Globex"));

        var result = await sut.ListAsync(new PartnerListQuery { Page = 1, PageSize = 10 });

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().AllBeOfType<PartnerResponse>();
    }

    [Fact]
    public async Task ListAsync_filters_by_tier()
    {
        HasPartners(NewPartner("Gold", tier: 1), NewPartner("Silver", tier: 2));

        var result = await sut.ListAsync(new PartnerListQuery { Tier = 2 });

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Silver");
    }

    [Fact]
    public async Task ListAsync_name_search_is_accent_and_case_insensitive()
    {
        HasPartners(NewPartner("Fundación Ávila"), NewPartner("Banco"));

        var result = await sut.ListAsync(new PartnerListQuery { Name = "avila" });

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Fundación Ávila");
    }

    [Fact]
    public async Task ListAsync_website_search_matches_substring()
    {
        HasPartners(
            NewPartner("A", web: "https://alpha.org"),
            NewPartner("B", web: "https://beta.org")
        );

        var result = await sut.ListAsync(new PartnerListQuery { Website = "beta" });

        result.Items.Should().ContainSingle().Which.Website.Should().Be("https://beta.org");
    }

    [Fact]
    public async Task ListAsync_honours_explicit_descending_sort()
    {
        HasPartners(NewPartner("Acme"), NewPartner("Zeta"), NewPartner("Mint"));

        var result = await sut.ListAsync(new PartnerListQuery { Sort = "-name" });

        result.Items.Select(p => p.Name).Should().ContainInOrder("Zeta", "Mint", "Acme");
    }

    [Fact]
    public async Task GetByIdAsync_returns_partner_when_found()
    {
        var partner = NewPartner();
        HasPartners(partner);

        var result = await sut.GetByIdAsync(partner.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(partner.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_when_missing()
    {
        HasPartners();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.PartnerNotFound);
    }

    [Fact]
    public async Task CreateAsync_fails_when_thumbnail_missing_and_does_not_persist()
    {
        ThumbnailExists(false);
        var request = new CreatePartnerRequest(
            "  Acme  ",
            new DateOnly(2024, 1, 1),
            1,
            " https://acme.test ",
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(request, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        await partners.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task CreateAsync_persists_trimmed_normalized_partner()
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

        var result = await sut.CreateAsync(request, caller);

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
    public async Task CreateAsync_stores_null_website_when_blank()
    {
        ThumbnailExists(true);
        var request = new CreatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            0,
            "   ",
            Guid.NewGuid()
        );

        var result = await sut.CreateAsync(request, Guid.NewGuid());

        result.Value.Website.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_returns_not_found_when_partner_missing()
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

        var result = await sut.UpdateAsync(Guid.NewGuid(), request, Guid.NewGuid());

        result.Error!.Code.Should().Be(ErrorCode.PartnerNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_returns_bad_request_when_thumbnail_missing()
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

        var result = await sut.UpdateAsync(partner.Id, request, Guid.NewGuid());

        result.Error!.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_mutates_and_persists()
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

        var result = await sut.UpdateAsync(partner.Id, request, caller);

        result.IsSuccess.Should().BeTrue();
        partner.Name.Should().Be("New");
        partner.Tier.Should().Be(5);
        partner.UpdatedBy.Should().Be(caller);
        partner.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_replacing_thumbnail_cleans_up_the_previous_file_after_save()
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

        var result = await sut.UpdateAsync(partner.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(previousThumbnailId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_keeping_the_same_thumbnail_does_not_clean_up()
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

        var result = await sut.UpdateAsync(partner.Id, request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    [Fact]
    public async Task DeleteAsync_returns_not_found_when_partner_missing()
    {
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Partner?)null);

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.Error!.Code.Should().Be(ErrorCode.PartnerNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await fileService.DidNotReceiveWithAnyArgs().DeleteIfOrphanedAsync(default, default);
    }

    [Fact]
    public async Task DeleteAsync_removes_saves_and_cleans_up_the_thumbnail()
    {
        var partner = NewPartner();
        partners
            .FindAsync(Arg.Any<Expression<Func<Partner, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(partner);

        var result = await sut.DeleteAsync(partner.Id);

        result.IsSuccess.Should().BeTrue();
        partners.Received(1).Remove(partner);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await fileService
            .Received(1)
            .DeleteIfOrphanedAsync(partner.ThumbnailId, Arg.Any<CancellationToken>());
    }
}
