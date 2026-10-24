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

public sealed class GetLegalPolicyQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetLegalPolicyQueryHandler sut;

    public GetLegalPolicyQueryHandlerTests()
    {
        sut = new GetLegalPolicyQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncPublishedPolicyReturnsTheVersionInForce()
    {
        store.LegalPolicies.AddRange([
            Policy(LegalPolicyKind.Privacy, 1, "privacy"),
            Policy(LegalPolicyKind.Cookies, 3, "cookies"),
        ]);

        var result = await sut.HandleAsync(
            new GetLegalPolicyQuery(LegalPolicyKind.Cookies),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new LegalPolicyResponse(LegalPolicyKind.Cookies, 3, "cookies", June));
    }

    [Fact]
    public async Task HandleAsyncUnpublishedPolicyReturnsNotFound()
    {
        store.LegalPolicies.Add(Policy(LegalPolicyKind.Privacy, 1, "privacy"));

        var result = await sut.HandleAsync(
            new GetLegalPolicyQuery(LegalPolicyKind.Cookies),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.LegalPolicyNotFound);
    }
}
