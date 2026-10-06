using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Security;

public sealed class ActingUserPolicyTests
{
    private readonly FakeReadStore store = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly ActingUserPolicy sut;

    public ActingUserPolicyTests()
    {
        sut = ActingUsers.Policy(currentUser, store);
    }

    private UserId PersonWithGuardian(UserId guardianId)
    {
        var personId = Guid.NewGuid();
        store.AddDependent(personId, guardianId.Value);
        return UserId.From(personId);
    }

    [Fact]
    public async Task EnsureMayActForAsyncThemselvesSucceeds()
    {
        var result = await sut.EnsureMayActForAsync(
            currentUser.Id!.Value,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        store.ReadsOf<UserRow>().Should().Be(0);
    }

    [Fact]
    public async Task EnsureMayActForAsyncTheirDependentSucceeds()
    {
        var child = PersonWithGuardian(currentUser.Id!.Value);

        var result = await sut.EnsureMayActForAsync(child, TestContext.Current.CancellationToken);

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

        var result = await sut.EnsureMayActForAsync(child, TestContext.Current.CancellationToken);

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
