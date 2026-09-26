using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class DisposableEmailDomainRefresherTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private readonly StubHttpMessageHandler source = new();

    private static string GenuineList(params string[] extraLines)
    {
        return string.Join(
            '\n',
            Enumerable
                .Range(0, DisposableEmailDomainList.MinDomains)
                .Select(index => $"disposable{index}.test")
                .Concat(extraLines)
        );
    }

    private DisposableEmailDomainRefresher Build()
    {
        var options = new DisposableEmailDomainOptions();
        return new DisposableEmailDomainRefresher(
            new DisposableEmailDomainDownloader(source, options),
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<DisposableEmailDomainRefresher>.Instance
        );
    }

    private Task<List<string>> StoredAsync()
    {
        return Factory.QueryAsync(db =>
            db.Set<DisposableEmailDomain>().Select(entry => entry.Domain).ToListAsync(Ct)
        );
    }

    [Fact]
    public async Task RefreshAsyncDownloadedListRefusesRegistrationAtItsDomains()
    {
        source.Enqueue(GenuineList("Mailinator.com"));
        using var refresher = Build();

        var stored = await refresher.RefreshAsync(Ct);

        stored.Should().BeTrue();
        (await StoredAsync())
            .Should()
            .HaveCount(DisposableEmailDomainList.MinDomains + 1)
            .And.Contain("mailinator.com");
        var response = await CreateClient()
            .PostJsonAsync(
                "/api/auth/register",
                new RegisterRequest(
                    "Nadia",
                    "Nueva",
                    "nadia@inbox.mailinator.com",
                    "+34600000099",
                    TestSeedData.Password,
                    "87654321X",
                    Gender.Female,
                    false,
                    null
                ),
                Ct
            );
        await response.ShouldBeBadRequestAsync(ErrorCode.DisposableEmailNotAllowed);
    }

    [Fact]
    public async Task RefreshAsyncFailedOrRejectedDownloadsKeepTheLastValidList()
    {
        source.Enqueue(GenuineList("mailinator.com"));
        source.Enqueue("Not Found", HttpStatusCode.NotFound);
        source.Enqueue("<!DOCTYPE html><html><body>Moved</body></html>");
        source.Enqueue(GenuineList("gmail.com"));
        source.Enqueue("mailinator.com");
        using var refresher = Build();
        (await refresher.RefreshAsync(Ct)).Should().BeTrue();
        var lastValid = await StoredAsync();

        var outcomes = new List<bool>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            outcomes.Add(await refresher.RefreshAsync(Ct));
        }

        outcomes.Should().Equal(false, false, false, false);
        (await StoredAsync()).Should().BeEquivalentTo(lastValid);
    }
}
