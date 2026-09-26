using AwesomeAssertions;
using CodigoActivo.Infrastructure.Database.Repositories;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static CodigoActivo.IntegrationTests.Infrastructure.TestCancellation;

namespace CodigoActivo.IntegrationTests.Repositories;

public sealed class DisposableEmailDomainRepositoryTests(PostgresContainerFixture postgres)
    : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await using var db = postgres.CreateContext();
        await TestDatabase.TruncateAllTablesAsync(db);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task ReplaceAsync(params string[] domains)
    {
        await using var db = postgres.CreateContext();
        await new DisposableEmailDomainRepository(db).ReplaceAsync(
            domains.ToHashSet(StringComparer.Ordinal),
            Ct
        );
    }

    private async Task<bool> ContainsAnyAsync(params string[] domains)
    {
        await using var db = postgres.CreateContext();
        return await new DisposableEmailDomainRepository(db).ContainsAnyAsync(domains, Ct);
    }

    private async Task<List<string>> StoredAsync()
    {
        await using var db = postgres.CreateContext();
        return await db
            .DisposableEmailDomains.Select(entry => entry.Domain)
            .OrderBy(domain => domain)
            .ToListAsync(Ct);
    }

    private async Task<string> RowVersionAsync(string domain)
    {
        await using var db = postgres.CreateContext();
        return await db
            .Database.SqlQuery<string>(
                $"SELECT xmin::text AS \"Value\" FROM disposable_email_domains WHERE domain = {domain}"
            )
            .SingleAsync(Ct);
    }

    [Fact]
    public async Task ContainsAnyAsyncNoListStoredFindsNothing()
    {
        (await ContainsAnyAsync("mailinator.com")).Should().BeFalse();
    }

    [Fact]
    public async Task ContainsAnyAsyncListedNameAmongTheCandidatesIsFound()
    {
        await ReplaceAsync("mailinator.com", "guerrillamail.com");

        (await ContainsAnyAsync("inbox.mailinator.com", "mailinator.com")).Should().BeTrue();
        (await ContainsAnyAsync("inbox.example.test", "example.test")).Should().BeFalse();
        (await ContainsAnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ReplaceAsyncChangedListWritesOnlyTheDifferences()
    {
        await ReplaceAsync("kept.test", "removed.test");
        var keptVersion = await RowVersionAsync("kept.test");

        await ReplaceAsync("kept.test", "added.test");

        (await StoredAsync()).Should().Equal("added.test", "kept.test");
        (await RowVersionAsync("kept.test")).Should().Be(keptVersion);
    }

    [Fact]
    public async Task ReplaceAsyncIdenticalListLeavesEveryRowUntouched()
    {
        await ReplaceAsync("first.test", "second.test");
        var version = await RowVersionAsync("second.test");

        await ReplaceAsync("second.test", "first.test");

        (await StoredAsync()).Should().Equal("first.test", "second.test");
        (await RowVersionAsync("second.test")).Should().Be(version);
    }

    [Fact]
    public async Task ReplaceAsyncEmptyListClearsTheStoredOne()
    {
        await ReplaceAsync("first.test", "second.test");

        await ReplaceAsync();

        (await StoredAsync()).Should().BeEmpty();
    }
}
