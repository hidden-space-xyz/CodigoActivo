using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.Application.Legal.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Legal.Queries.LegalPolicyRows;

namespace CodigoActivo.UnitTests.Application.Legal.Queries;

public sealed class GetLegalPolicyVersionQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetLegalPolicyVersionQueryHandler sut;

    public GetLegalPolicyVersionQueryHandlerTests()
    {
        sut = new GetLegalPolicyVersionQueryHandler(store, new FakeQueryExecutor());
        var privacy = Policy(LegalPolicyKind.Privacy, 2, "second");
        var cookies = Policy(LegalPolicyKind.Cookies, 2, "cookies");
        store.LegalPolicies.AddRange([privacy, cookies]);
        store.LegalPolicyVersions.AddRange([
            Replaced(privacy, 1, "first", January),
            Replaced(cookies, 1, "old cookies", March),
        ]);
    }

    private Task<Result<LegalPolicyResponse>> GetAsync(LegalPolicyKind kind, int version)
    {
        return sut.HandleAsync(
            new GetLegalPolicyVersionQuery(kind, version),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncVersionInForceReturnsItsContent()
    {
        var result = await GetAsync(LegalPolicyKind.Privacy, 2);

        result
            .Value.Should()
            .Be(new LegalPolicyResponse(LegalPolicyKind.Privacy, 2, "second", June));
    }

    [Fact]
    public async Task HandleAsyncReplacedVersionReturnsItsContent()
    {
        var result = await GetAsync(LegalPolicyKind.Privacy, 1);

        result
            .Value.Should()
            .Be(new LegalPolicyResponse(LegalPolicyKind.Privacy, 1, "first", January));
    }

    [Theory]
    [InlineData(LegalPolicyKind.Privacy, 3)]
    [InlineData(LegalPolicyKind.Privacy, 0)]
    public async Task HandleAsyncUnknownVersionReturnsNotFound(LegalPolicyKind kind, int version)
    {
        var result = await GetAsync(kind, version);

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.LegalPolicyVersionNotFound);
    }
}
