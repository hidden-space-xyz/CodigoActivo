using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Logs;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetPersonalDataQueryHandlerTests
{
    private readonly ILogEntryReader logs = Substitute.For<ILogEntryReader>();
    private readonly GetPersonalDataQueryHandler sut;

    public GetPersonalDataQueryHandlerTests()
    {
        sut = new GetPersonalDataQueryHandler(
            new FakeReadStore(),
            new FakeQueryExecutor(),
            logs,
            new TestClock(),
            new TestCurrentUser()
        );
    }

    [Fact]
    public async Task HandleAsyncAccountThatNoLongerExistsReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetPersonalDataQuery(),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await logs.DidNotReceiveWithAnyArgs()
            .ListOwnAuditAsync(default, TestContext.Current.CancellationToken);
    }
}
