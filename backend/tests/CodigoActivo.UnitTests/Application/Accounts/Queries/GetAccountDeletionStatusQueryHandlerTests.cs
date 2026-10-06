using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetAccountDeletionStatusQueryHandlerTests
{
    private readonly GetAccountDeletionStatusQueryHandler sut = new();

    private Task<AccountDeletionStatusResponse> StatusAsync(Guid userId)
    {
        return sut.HandleAsync(
            new GetAccountDeletionStatusQuery(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncAnyOtherAccountIsAllowed()
    {
        var status = await StatusAsync(Guid.NewGuid());

        status.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncInitialAdministratorIsNotAllowed()
    {
        var status = await StatusAsync(KnownIds.Users.InitialAdministrator);

        status.Allowed.Should().BeFalse();
    }
}
