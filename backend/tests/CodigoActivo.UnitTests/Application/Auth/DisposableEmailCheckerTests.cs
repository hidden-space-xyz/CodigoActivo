using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Auth;

public sealed class DisposableEmailCheckerTests
{
    private readonly FakeDisposableEmailDomainRepository domains = new();
    private readonly DisposableEmailChecker sut;

    public DisposableEmailCheckerTests()
    {
        domains.Add("mailinator.com");
        sut = new DisposableEmailChecker(domains);
    }

    [Theory]
    [InlineData("ana@mailinator.com")]
    [InlineData("ana@inbox.mailinator.com")]
    [InlineData("ana@MAILINATOR.com.")]
    public async Task IsDisposableAsyncListedDomainOrSubdomainReturnsTrue(string email)
    {
        var disposable = await sut.IsDisposableAsync(email, TestContext.Current.CancellationToken);

        disposable.Should().BeTrue();
    }

    [Theory]
    [InlineData("ana@notmailinator.com")]
    [InlineData("ana@mailinator.com.es")]
    [InlineData("ana@example.test")]
    public async Task IsDisposableAsyncUnlistedDomainReturnsFalse(string email)
    {
        var disposable = await sut.IsDisposableAsync(email, TestContext.Current.CancellationToken);

        disposable.Should().BeFalse();
    }

    [Fact]
    public async Task IsDisposableAsyncLooksUpTheDomainAndItsParentsTogether()
    {
        await sut.IsDisposableAsync("ana@a.b.example.test", TestContext.Current.CancellationToken);

        domains
            .Lookups.Should()
            .ContainSingle()
            .Which.Should()
            .Equal("a.b.example.test", "b.example.test", "example.test");
    }

    [Fact]
    public async Task IsDisposableAsyncAddressWithoutDomainSkipsTheLookup()
    {
        var disposable = await sut.IsDisposableAsync("ana@", TestContext.Current.CancellationToken);

        disposable.Should().BeFalse();
        domains.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task IsDisposableAsyncEmptyListAcceptsEveryAddress()
    {
        domains.Clear();

        var disposable = await sut.IsDisposableAsync(
            "ana@mailinator.com",
            TestContext.Current.CancellationToken
        );

        disposable.Should().BeFalse();
    }
}
