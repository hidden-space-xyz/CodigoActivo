using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class ListUserStatusTypesQueryHandlerTests
{
    private readonly IUserStatusTypeRepository userStatusTypes =
        Substitute.For<IUserStatusTypeRepository>();
    private readonly ListUserStatusTypesQueryHandler sut;

    public ListUserStatusTypesQueryHandlerTests()
    {
        sut = new ListUserStatusTypesQueryHandler(
            userStatusTypes,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleStatusTypesProjectsOrderedByName()
    {
        userStatusTypes.HasStatusTypes(
            NewStatusType("Pending"),
            NewStatusType("Active"),
            NewStatusType("Blocked")
        );

        var result = await sut.HandleAsync(
            new ListUserStatusTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(s => s.Name).Should().ContainInOrder("Active", "Blocked", "Pending");
        result.Should().AllBeOfType<UserStatusTypeResponse>();
    }
}
