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

public sealed class ListLegalPolicyVersionsQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListLegalPolicyVersionsQueryHandler sut;

    public ListLegalPolicyVersionsQueryHandlerTests()
    {
        sut = new ListLegalPolicyVersionsQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncPublishedPolicyListsEveryVersionNewestFirst()
    {
        var privacy = Policy(LegalPolicyKind.Privacy, 3, "third");
        var cookies = Policy(LegalPolicyKind.Cookies, 2, "cookies");
        store.LegalPolicies.AddRange([privacy, cookies]);
        store.LegalPolicyVersions.AddRange([
            Replaced(privacy, 1, "first", January),
            Replaced(cookies, 1, "cookies", January),
            Replaced(privacy, 2, "second", March),
        ]);

        var result = await sut.HandleAsync(
            new ListLegalPolicyVersionsQuery(LegalPolicyKind.Privacy),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Equal(
                new LegalPolicyVersionResponse(3, June),
                new LegalPolicyVersionResponse(2, March),
                new LegalPolicyVersionResponse(1, January)
            );
    }

    [Fact]
    public async Task HandleAsyncUnpublishedPolicyReturnsNotFound()
    {
        store.LegalPolicies.Add(Policy(LegalPolicyKind.Cookies, 1, "cookies"));

        var result = await sut.HandleAsync(
            new ListLegalPolicyVersionsQuery(LegalPolicyKind.Privacy),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.LegalPolicyNotFound);
    }
}
