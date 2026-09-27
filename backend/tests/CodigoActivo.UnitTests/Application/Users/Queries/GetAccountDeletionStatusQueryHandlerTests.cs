using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Constants;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class GetAccountDeletionStatusQueryHandlerTests
{
    private readonly GetAccountDeletionStatusQueryHandler sut = new();

    private Task<AccountDeletionStatusResponse> StatusAsync(Guid userId)
    {
        return sut.HandleAsync(
            new GetAccountDeletionStatusQuery(userId),
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
        var status = await StatusAsync(SeedIds.Users.InitialAdministrator);

        status.Allowed.Should().BeFalse();
    }
}
