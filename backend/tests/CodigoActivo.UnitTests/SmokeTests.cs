using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests;

/// <summary>
/// Validates the unit toolchain end-to-end: xUnit v3 discovery/run, AwesomeAssertions, NSubstitute,
/// and the shared <see cref="FakeQueryExecutor"/>. Real coverage lives in the per-component suites.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void Error_implicitly_converts_to_a_failed_result()
    {
        Result result = Error.NotFound(ErrorCode.UserNotFound);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task Substituted_repository_returns_configured_value()
    {
        var users = Substitute.For<IUserRepository>();
        users.EmailExistsAsync("ada@example.com").Returns(true);

        var exists = await users.EmailExistsAsync("ada@example.com");

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task FakeQueryExecutor_pages_over_an_in_memory_source()
    {
        var executor = new FakeQueryExecutor();
        var source = Enumerable.Range(1, 30).AsQueryable();

        var page = await executor.ToPagedAsync(source, page: 2, pageSize: 10);

        page.Total.Should().Be(30);
        page.Page.Should().Be(2);
        page.Items.Should().HaveCount(10);
        page.Items[0].Should().Be(11);
    }
}
