using System.Collections.Concurrent;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace CodigoActivo.Application.Services;

public sealed class ResponseCacheService(IMemoryCache cache) : IResponseCacheService
{
    private const int GateCount = 64;

    private readonly ConcurrentDictionary<string, CancellationTokenSource> groupTokens = new();
    private readonly SemaphoreSlim[] gates = CreateGates(GateCount);

    public async Task<object?> GetOrCreateAsync(
        string key,
        string group,
        TimeSpan ttl,
        Func<Task<object?>> factory
    )
    {
        if (cache.TryGetValue(key, out object? cached))
        {
            return cached;
        }

        var gate = GateFor(key);
        await gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var value = await factory();
            if (value is not null)
            {
                Store(key, value, group, ttl);
            }

            return value;
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateGroups(params string[] groups)
    {
        foreach (var group in groups)
        {
            if (groupTokens.TryRemove(group, out var cts))
            {
                cts.Cancel();
            }
        }
    }

    private void Store(string key, object value, string group, TimeSpan ttl)
    {
        var cts = groupTokens.GetOrAdd(group, _ => new CancellationTokenSource());
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
        }.AddExpirationToken(new CancellationChangeToken(cts.Token));

        cache.Set(key, value, options);
    }

    private SemaphoreSlim GateFor(string key)
    {
        var index = (uint)key.GetHashCode(StringComparison.Ordinal) % (uint)gates.Length;
        return gates[index];
    }

    private static SemaphoreSlim[] CreateGates(int count)
    {
        var result = new SemaphoreSlim[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = new SemaphoreSlim(1, 1);
        }

        return result;
    }
}
