using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts;

public sealed class AccountLockTests
{
    private static readonly Result Gone = Error.NotFound(ApplicationErrorCode.UserNotFound);

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly AccountLock sut;

    public AccountLockTests()
    {
        sut = AccountLocks.Create(users, uow);
    }

    [Fact]
    public async Task RunAsyncAccountGoneAnswersWithoutRunningTheChange()
    {
        var account = NewUser();
        users.LockAsync(account, Arg.Any<CancellationToken>()).Returns(false);
        var ran = false;

        var result = await sut.RunAsync(
            account,
            _ =>
            {
                ran = true;
                return Task.FromResult(Result.Success());
            },
            Gone,
            TestContext.Current.CancellationToken
        );

        result.Should().BeSameAs(Gone);
        ran.Should().BeFalse();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RunAsyncSucceededChangeIsSavedAfterTheAccountIsLocked()
    {
        var account = NewUser();

        var result = await sut.RunAsync(
            account,
            _ => Task.FromResult(Result.Success()),
            Gone,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _ = users.LockAsync(account, Arg.Any<CancellationToken>());
            _ = uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RunAsyncFailedChangeIsNotSaved()
    {
        Result refused = Error.Validation(ApplicationErrorCode.OtpInvalidOrExpired);

        var result = await sut.RunAsync(
            NewUser(),
            _ => Task.FromResult(refused),
            Gone,
            TestContext.Current.CancellationToken
        );

        result.Should().BeSameAs(refused);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
