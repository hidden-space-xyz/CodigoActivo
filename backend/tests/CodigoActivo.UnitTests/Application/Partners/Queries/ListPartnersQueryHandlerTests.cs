using AwesomeAssertions;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Queries;

public sealed class ListPartnersQueryHandlerTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly ListPartnersQueryHandler sut;

    public ListPartnersQueryHandlerTests()
    {
        sut = new ListPartnersQueryHandler(
            partners,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_TierFilter_ReturnsMatchingTier()
    {
        partners.HasPartners(NewPartner("Gold", tier: 1), NewPartner("Silver", tier: 2));

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Tier = 2 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Silver");
    }

    [Fact]
    public async Task HandleAsync_FromDateRangeFilter_KeepsPartnersWithinInclusiveBounds()
    {
        partners.HasPartners(
            NewPartner("Antes", fromDate: new DateOnly(2019, 12, 31)),
            NewPartner("Inicio", fromDate: new DateOnly(2020, 1, 1)),
            NewPartner("Fin", fromDate: new DateOnly(2023, 6, 30)),
            NewPartner("Despues", fromDate: new DateOnly(2023, 7, 1))
        );

        var result = await sut.HandleAsync(
            new ListPartnersQuery(
                new PartnerListQuery
                {
                    FromDateFrom = new DateOnly(2020, 1, 1),
                    FromDateTo = new DateOnly(2023, 6, 30),
                }
            ),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(p => p.Name).Should().BeEquivalentTo("Inicio", "Fin");
    }

    [Fact]
    public async Task HandleAsync_FromDateFromFilter_ExcludesEarlierPartners()
    {
        partners.HasPartners(
            NewPartner("Viejo", fromDate: new DateOnly(2018, 5, 5)),
            NewPartner("Nuevo", fromDate: new DateOnly(2024, 5, 5))
        );

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { FromDateFrom = new DateOnly(2020, 1, 1) }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Nuevo");
    }

    [Fact]
    public async Task HandleAsync_NameSearch_IsAccentAndCaseInsensitive()
    {
        partners.HasPartners(NewPartner("Fundación Ávila"), NewPartner("Banco"));

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Name = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Fundación Ávila");
    }

    [Fact]
    public async Task HandleAsync_WebsiteSearch_MatchesSubstring()
    {
        partners.HasPartners(
            NewPartner("A", web: "https://alpha.org"),
            NewPartner("B", web: "https://beta.org")
        );

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Website = "beta" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Website.Should().Be("https://beta.org");
    }

    [Fact]
    public async Task HandleAsync_ExplicitDescendingSort_OrdersDescending()
    {
        partners.HasPartners(NewPartner("Acme"), NewPartner("Zeta"), NewPartner("Mint"));

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Sort = "-name" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(p => p.Name).Should().ContainInOrder("Zeta", "Mint", "Acme");
    }

    [Fact]
    public async Task HandleAsync_NoSortSpecified_OrdersByTierAscendingThenFromDateDescending()
    {
        var tier1Newer = NewPartner("Tier1Newer", tier: 1);
        tier1Newer.FromDate = new DateOnly(2023, 6, 1);
        var tier1Older = NewPartner("Tier1Older", tier: 1);
        tier1Older.FromDate = new DateOnly(2020, 6, 1);
        var tier2 = NewPartner("Tier2", tier: 2);
        tier2.FromDate = new DateOnly(2025, 1, 1);
        var tier3 = NewPartner("Tier3", tier: 3);
        tier3.FromDate = new DateOnly(2019, 1, 1);
        partners.HasPartners(tier2, tier3, tier1Older, tier1Newer);

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery()),
            TestContext.Current.CancellationToken
        );

        result
            .Items.Select(p => p.Name)
            .Should()
            .ContainInOrder("Tier1Newer", "Tier1Older", "Tier2", "Tier3");
    }
}
