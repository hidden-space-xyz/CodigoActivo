using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Database;
using CodigoActivo.Infrastructure.Database.Context;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Database;

public sealed class QueryExecutorTests : IDisposable
{
    private readonly CodigoActivoDbContext context;
    private readonly QueryExecutor sut = new();

    public QueryExecutorTests()
    {
        var options = new DbContextOptionsBuilder<CodigoActivoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        context = new CodigoActivoDbContext(options);
    }

    public void Dispose() => context.Dispose();

    private async Task SeedPartnersAsync(params string[] names)
    {
        foreach (var name in names)
        {
            context.Partners.Add(
                new Partner
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Tier = 1,
                    FromDate = new DateOnly(2024, 1, 1),
                    ThumbnailId = Guid.NewGuid(),
                    CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    CreatedBy = Guid.NewGuid(),
                }
            );
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ToPagedAsync_returns_total_and_the_requested_page_slice()
    {
        await SeedPartnersAsync("A", "B", "C", "D", "E");
        var query = context.Partners.OrderBy(p => p.Name);

        var result = await sut.ToPagedAsync(query, page: 2, pageSize: 2);

        result.Total.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Select(p => p.Name).Should().ContainInOrder("C", "D");
    }

    [Fact]
    public async Task ToPagedAsync_returns_empty_slice_past_the_last_page()
    {
        await SeedPartnersAsync("A", "B");

        var result = await sut.ToPagedAsync(context.Partners, page: 5, pageSize: 10);

        result.Total.Should().Be(2);
        result.Items.Should().BeEmpty();
    }
}
