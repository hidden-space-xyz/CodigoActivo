using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class GetAccountDeletionStatusQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly GetAccountDeletionStatusQueryHandler sut;

    public GetAccountDeletionStatusQueryHandlerTests()
    {
        sut = new GetAccountDeletionStatusQueryHandler(users, new FakeQueryExecutor());
    }

    private Task<AccountDeletionStatusResponse> StatusAsync(Guid userId, bool isAdmin)
    {
        return sut.HandleAsync(
            new GetAccountDeletionStatusQuery(userId, isAdmin),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncNonAdministratorIsAllowedWithoutQuerying()
    {
        var status = await StatusAsync(Guid.NewGuid(), isAdmin: false);

        status.Allowed.Should().BeTrue();
        users.DidNotReceive().Query();
    }

    [Fact]
    public async Task HandleAsyncLastAdministratorIsNotAllowed()
    {
        var admin = NewUser(isAdmin: true);
        users.HasUsers(admin, NewUser());

        var status = await StatusAsync(admin.Id, isAdmin: true);

        status.Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncAdministratorWithAnotherAdministratorIsAllowed()
    {
        var admin = NewUser(isAdmin: true);
        users.HasUsers(admin, NewUser(isAdmin: true));

        var status = await StatusAsync(admin.Id, isAdmin: true);

        status.Allowed.Should().BeTrue();
    }
}
