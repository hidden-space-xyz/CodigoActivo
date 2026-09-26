using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class FakeDisposableEmailDomainRepository : IDisposableEmailDomainRepository
{
    private readonly Lock gate = new();
    private readonly HashSet<string> domains = new(StringComparer.Ordinal);
    private readonly List<IReadOnlyCollection<string>> lookups = [];

    public IReadOnlyCollection<string> Domains
    {
        get
        {
            lock (gate)
            {
                return [.. domains.Order(StringComparer.Ordinal)];
            }
        }
    }

    public IReadOnlyList<IReadOnlyCollection<string>> Lookups
    {
        get
        {
            lock (gate)
            {
                return [.. lookups];
            }
        }
    }

    public void Add(params string[] entries)
    {
        lock (gate)
        {
            domains.UnionWith(entries);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            domains.Clear();
        }
    }

    public Task<bool> ContainsAnyAsync(
        IReadOnlyCollection<string> domains,
        CancellationToken ct = default
    )
    {
        lock (gate)
        {
            lookups.Add([.. domains]);
            return Task.FromResult(domains.Any(this.domains.Contains));
        }
    }

    public Task ReplaceAsync(IReadOnlySet<string> domains, CancellationToken ct = default)
    {
        lock (gate)
        {
            this.domains.Clear();
            this.domains.UnionWith(domains);
        }

        return Task.CompletedTask;
    }
}
