using AwesomeAssertions;
using CodigoActivo.Application.Partners.Contracts;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Queries;

public sealed class ListPartnersQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListPartnersQueryHandler sut;

    public ListPartnersQueryHandlerTests()
    {
        sut = new ListPartnersQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncTierFilterReturnsMatchingTier()
    {
        store.Partners.AddRange([NewPartnerRow("Gold", tier: 1), NewPartnerRow("Silver", tier: 2)]);

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Tier = 2 }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Silver");
    }

    [Fact]
    public async Task HandleAsyncFromDateRangeFilterKeepsPartnersWithinInclusiveBounds()
    {
        store.Partners.AddRange([
            NewPartnerRow("Antes", fromDate: new DateOnly(2019, 12, 31)),
            NewPartnerRow("Inicio", fromDate: new DateOnly(2020, 1, 1)),
            NewPartnerRow("Fin", fromDate: new DateOnly(2023, 6, 30)),
            NewPartnerRow("Despues", fromDate: new DateOnly(2023, 7, 1)),
        ]);

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
    public async Task HandleAsyncFromDateFromFilterExcludesEarlierPartners()
    {
        store.Partners.AddRange([
            NewPartnerRow("Viejo", fromDate: new DateOnly(2018, 5, 5)),
            NewPartnerRow("Nuevo", fromDate: new DateOnly(2024, 5, 5)),
        ]);

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { FromDateFrom = new DateOnly(2020, 1, 1) }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Nuevo");
    }

    [Fact]
    public async Task HandleAsyncNameSearchIsAccentAndCaseInsensitive()
    {
        store.Partners.AddRange([NewPartnerRow("Fundación Ávila"), NewPartnerRow("Banco")]);

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Name = "avila" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Name.Should().Be("Fundación Ávila");
    }

    [Fact]
    public async Task HandleAsyncWebsiteSearchMatchesSubstring()
    {
        store.Partners.AddRange([
            NewPartnerRow("A", web: "https://alpha.org"),
            NewPartnerRow("B", web: "https://beta.org"),
        ]);

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Website = "beta" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Website.Should().Be("https://beta.org");
    }

    [Fact]
    public async Task HandleAsyncExplicitDescendingSortOrdersDescending()
    {
        store.Partners.AddRange([
            NewPartnerRow("Acme"),
            NewPartnerRow("Zeta"),
            NewPartnerRow("Mint"),
        ]);

        var result = await sut.HandleAsync(
            new ListPartnersQuery(new PartnerListQuery { Sort = "-name" }),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(p => p.Name).Should().ContainInOrder("Zeta", "Mint", "Acme");
    }

    [Fact]
    public async Task HandleAsyncNoSortSpecifiedOrdersByTierAscendingThenFromDateDescending()
    {
        var tier1Newer = NewPartnerRow("Tier1Newer", tier: 1, fromDate: new DateOnly(2023, 6, 1));
        var tier1Older = NewPartnerRow("Tier1Older", tier: 1, fromDate: new DateOnly(2020, 6, 1));
        var tier2 = NewPartnerRow("Tier2", tier: 2, fromDate: new DateOnly(2025, 1, 1));
        var tier3 = NewPartnerRow("Tier3", tier: 3, fromDate: new DateOnly(2019, 1, 1));
        store.Partners.AddRange([tier2, tier3, tier1Older, tier1Newer]);

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
