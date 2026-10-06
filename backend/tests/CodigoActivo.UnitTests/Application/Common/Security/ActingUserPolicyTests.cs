using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Security;

public sealed class ActingUserPolicyTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly ActingUserPolicy sut;

    public ActingUserPolicyTests()
    {
        sut = new ActingUserPolicy(currentUser, users);
    }

    private User PersonWithGuardian(UserId? guardianId)
    {
        var person = Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Lovelace",
                ParentId = guardianId?.Value,
            }
        );
        users.GetByIdAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);
        return person;
    }

    [Fact]
    public async Task EnsureMayActForAsyncThemselvesSucceeds()
    {
        var result = await sut.EnsureMayActForAsync(
            currentUser.Id!.Value,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnsureMayActForAsyncTheirDependentSucceeds()
    {
        var child = PersonWithGuardian(currentUser.Id);

        var result = await sut.EnsureMayActForAsync(
            child.Id,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureMayActForAsyncAnyoneAsAdministratorSucceeds()
    {
        currentUser.IsAdmin = true;

        var result = await sut.EnsureMayActForAsync(
            UserId.New(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureMayActForAsyncSomeoneElsesDependentIsForbidden()
    {
        var child = PersonWithGuardian(UserId.New());

        var result = await sut.EnsureMayActForAsync(
            child.Id,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActingForAnotherUserForbidden);
    }

    [Fact]
    public async Task EnsureMayActForAsyncUnknownPersonIsForbidden()
    {
        var result = await sut.EnsureMayActForAsync(
            UserId.New(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActingForAnotherUserForbidden);
    }
}
