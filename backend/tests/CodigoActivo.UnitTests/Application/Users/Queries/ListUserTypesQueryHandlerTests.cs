using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class ListUserTypesQueryHandlerTests
{
    private readonly IUserTypeRepository userTypes = Substitute.For<IUserTypeRepository>();
    private readonly ListUserTypesQueryHandler sut;

    public ListUserTypesQueryHandlerTests()
    {
        sut = new ListUserTypesQueryHandler(
            userTypes,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncMultipleUserTypesProjectsOrderedByName()
    {
        userTypes.HasUserTypes(
            NewUserType("Volunteer"),
            NewUserType("Admin"),
            NewUserType("Member")
        );

        var result = await sut.HandleAsync(
            new ListUserTypesQuery(),
            TestContext.Current.CancellationToken
        );

        result.Select(t => t.Name).Should().ContainInOrder("Admin", "Member", "Volunteer");
        result.Should().AllBeOfType<UserTypeResponse>();
    }
}
