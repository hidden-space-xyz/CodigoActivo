using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Inventory.Contracts;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Inventory.Contracts;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class InventoryControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Categories = "/api/inventory/categories";

    private static CreateInventoryCategoryRequest Shirts(string name = "Camisetas")
    {
        return new CreateInventoryCategoryRequest(
            name,
            [
                new InventoryColumnRequest(
                    null,
                    "Talla",
                    InventoryColumnType.List,
                    true,
                    [
                        new InventoryColumnOptionRequest(null, "S", "#112233"),
                        new InventoryColumnOptionRequest(null, "M", "#445566"),
                    ]
                ),
                new InventoryColumnRequest(null, "Color", InventoryColumnType.Text, false, []),
                new InventoryColumnRequest(
                    null,
                    "Cantidad",
                    InventoryColumnType.Quantity,
                    true,
                    []
                ),
            ]
        );
    }

    private static UpdateInventoryCategoryRequest Redefined(
        InventoryCategoryResponse category,
        Func<InventoryColumnResponse, InventoryColumnRequest?> change
    )
    {
        return new UpdateInventoryCategoryRequest(
            category.Name,
            [.. category.Columns.Select(change).OfType<InventoryColumnRequest>()]
        );
    }

    private static InventoryColumnRequest Kept(InventoryColumnResponse column)
    {
        return new InventoryColumnRequest(
            column.Id,
            column.Name,
            column.Type,
            column.Required,
            [.. column.Options.Select(o => new InventoryColumnOptionRequest(o.Id, o.Name, o.Color))]
        );
    }

    private static InventoryColumnResponse Column(InventoryCategoryResponse category, string name)
    {
        return category.Columns.Single(column => column.Name == name);
    }

    private static async Task<InventoryCategoryResponse> CreateAsync(HttpClient client)
    {
        var response = await client.PostJsonAsync(Categories, Shirts(), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync<InventoryCategoryResponse>(Ct))!;
    }

    private static async Task<InventoryEntryResponse> AddAsync(
        HttpClient client,
        InventoryCategoryResponse category,
        string size,
        int quantity,
        string? color = null
    )
    {
        var values = new Dictionary<Guid, string>
        {
            [Column(category, "Talla").Id] = Column(category, "Talla")
                .Options.Single(option => option.Name == size)
                .Id.ToString(),
        };
        if (color is not null)
        {
            values[Column(category, "Color").Id] = color;
        }

        var response = await client.PostJsonAsync(
            $"{Categories}/{category.Id}/entries",
            new AddInventoryEntryRequest(quantity, values),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync<InventoryEntryResponse>(Ct))!;
    }

    [Fact]
    public async Task CreateAdminReturns201WithTheColumnsInOrder()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PostJsonAsync(Categories, Shirts(), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.ReadJsonAsync<InventoryCategoryResponse>(Ct))!;
        response.Headers.Location!.OriginalString.Should().Be($"{Categories}/{created.Id}");
        created.Name.Should().Be("Camisetas");
        created.Columns.Select(column => column.Name).Should().Equal("Talla", "Color", "Cantidad");
        Column(created, "Talla").Options.Select(option => option.Name).Should().Equal("S", "M");
        created.EntryCount.Should().Be(0);
        created.Total.Should().Be(0);
    }

    [Fact]
    public async Task CreateNameTakenIgnoringCaseReturnsConflict()
    {
        var client = await LoginAsAdminAsync();
        await CreateAsync(client);

        var response = await client.PostJsonAsync(Categories, Shirts(" CAMISETAS "), Ct);

        await response.ShouldBeConflictAsync(ErrorCode.InventoryCategoryNameAlreadyExists);
    }

    [Fact]
    public async Task CreateSecondQuantityColumnReturnsValidationError()
    {
        var client = await LoginAsAdminAsync();
        var request = Shirts() with
        {
            Columns =
            [
                .. Shirts().Columns,
                new InventoryColumnRequest(
                    null,
                    "Repuestos",
                    InventoryColumnType.Quantity,
                    false,
                    []
                ),
            ],
        };

        var response = await client.PostJsonAsync(Categories, request, Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task EntriesAdminAddsChangesAndRemovesThemUpdatingTheTotal()
    {
        var client = await LoginAsAdminAsync();
        var category = await CreateAsync(client);
        var small = await AddAsync(client, category, "S", 30, " Negra ");
        var medium = await AddAsync(client, category, "M", 12);

        small.Quantity.Should().Be(30);
        small.Values[Column(category, "Color").Id].Should().Be("Negra");

        var updated = await client.PutJsonAsync(
            $"{Categories}/{category.Id}/entries/{medium.Id}",
            new UpdateInventoryEntryRequest(15, medium.Values),
            Ct
        );
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.ReadJsonAsync<InventoryEntryResponse>(Ct))!.Quantity.Should().Be(15);

        var list = await client.GetAsync(TestUri.Rel(Categories), Ct);
        var page = (await list.ReadJsonAsync<PagedResult<InventoryCategorySummaryResponse>>(Ct))!;
        page.Items.Should()
            .Equal(new InventoryCategorySummaryResponse(category.Id, "Camisetas", 2, 45));

        var deleted = await client.DeleteWithCsrfAsync(
            $"{Categories}/{category.Id}/entries/{small.Id}",
            Ct
        );
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entries = await client.GetAsync(TestUri.Rel($"{Categories}/{category.Id}/entries"), Ct);
        (await entries.ReadJsonAsync<List<InventoryEntryResponse>>(Ct))!
            .Should()
            .ContainSingle()
            .Which.Id.Should()
            .Be(medium.Id);
    }

    [Fact]
    public async Task ListSortedByTotalOrdersTheCategoriesByIt()
    {
        var client = await LoginAsAdminAsync();
        var shirts = await CreateAsync(client);
        await AddAsync(client, shirts, "S", 5);
        var created = await client.PostJsonAsync(Categories, Shirts("Sudaderas"), Ct);
        var hoodies = (await created.ReadJsonAsync<InventoryCategoryResponse>(Ct))!;
        await AddAsync(client, hoodies, "M", 40);

        var response = await client.GetAsync(TestUri.Rel($"{Categories}?sort=-total"), Ct);

        var page = (
            await response.ReadJsonAsync<PagedResult<InventoryCategorySummaryResponse>>(Ct)
        )!;
        page.Items.Select(item => (item.Name, item.Total))
            .Should()
            .Equal(("Sudaderas", 40), ("Camisetas", 5));
    }

    [Fact]
    public async Task AddEntryRequiredValueMissingReturnsValidationError()
    {
        var client = await LoginAsAdminAsync();
        var category = await CreateAsync(client);

        var response = await client.PostJsonAsync(
            $"{Categories}/{category.Id}/entries",
            new AddInventoryEntryRequest(3, new Dictionary<Guid, string>()),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task UpdateEntryUnknownReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();
        var category = await CreateAsync(client);

        var response = await client.PutJsonAsync(
            $"{Categories}/{category.Id}/entries/{Guid.NewGuid()}",
            new UpdateInventoryEntryRequest(1, new Dictionary<Guid, string>()),
            Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.InventoryEntryNotFound);
    }

    [Fact]
    public async Task UpdateCategoryRemovedColumnLosesItsValues()
    {
        var client = await LoginAsAdminAsync();
        var category = await CreateAsync(client);
        var entry = await AddAsync(client, category, "S", 4, "Negra");

        var response = await client.PutJsonAsync(
            $"{Categories}/{category.Id}",
            Redefined(category, column => column.Name == "Color" ? null : Kept(column)) with
            {
                Name = "Camisetas 2026",
            },
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.ReadJsonAsync<InventoryCategoryResponse>(Ct))!;
        updated.Name.Should().Be("Camisetas 2026");
        updated.Columns.Select(column => column.Name).Should().Equal("Talla", "Cantidad");
        var entries = await client.GetAsync(TestUri.Rel($"{Categories}/{category.Id}/entries"), Ct);
        (await entries.ReadJsonAsync<List<InventoryEntryResponse>>(Ct))!
            .Single()
            .Values.Keys.Should()
            .Equal(Column(category, "Talla").Id);
        entry.Values.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateCategoryChangesEntriesForbidReturnConflicts()
    {
        var client = await LoginAsAdminAsync();
        var category = await CreateAsync(client);
        await AddAsync(client, category, "S", 4);
        var url = $"{Categories}/{category.Id}";

        var retyped = await client.PutJsonAsync(
            url,
            Redefined(
                category,
                column =>
                    column.Name == "Cantidad"
                        ? Kept(column) with
                        {
                            Type = InventoryColumnType.Number,
                        }
                        : Kept(column)
            ),
            Ct
        );
        var withoutOption = await client.PutJsonAsync(
            url,
            Redefined(
                category,
                column =>
                    column.Name == "Talla"
                        ? Kept(column) with
                        {
                            Options = [.. Kept(column).Options.Skip(1)],
                        }
                        : Kept(column)
            ),
            Ct
        );
        var required = await client.PutJsonAsync(
            url,
            Redefined(
                category,
                column =>
                    column.Name == "Color" ? Kept(column) with { Required = true } : Kept(column)
            ),
            Ct
        );

        await retyped.ShouldBeConflictAsync(ErrorCode.InventoryColumnTypeInUse);
        await withoutOption.ShouldBeConflictAsync(ErrorCode.InventoryColumnOptionInUse);
        await required.ShouldBeConflictAsync(ErrorCode.InventoryColumnRequiredWithEmptyEntries);
    }

    [Fact]
    public async Task DeleteCategoryAdminRemovesItWithItsEntries()
    {
        var client = await LoginAsAdminAsync();
        var category = await CreateAsync(client);
        await AddAsync(client, category, "M", 2);

        var deleted = await client.DeleteWithCsrfAsync($"{Categories}/{category.Id}", Ct);

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var response = await client.GetAsync(
            TestUri.Rel($"{Categories}/{category.Id}/entries"),
            Ct
        );
        await response.ShouldBeNotFoundAsync(ErrorCode.InventoryCategoryNotFound);
    }
}
