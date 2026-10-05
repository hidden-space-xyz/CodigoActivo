using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts;

public sealed class EmailClaimsTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly EmailClaims sut;

    public EmailClaimsTests()
    {
        sut = new EmailClaims(
            AccountErasers.Create(users, deletedAccounts, erasureStore, uow),
            uow
        );
    }

    [Fact]
    public async Task TryCommitAsyncHolderThatOwnsTheEmailThrowsWithoutCommitting()
    {
        var holder = NewUser(statusId: SeedIds.UserStatusTypes.Active);

        var act = () =>
            sut.TryCommitAsync(
                NewUser(),
                holder,
                clock.UtcNow,
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<ArgumentException>();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TryCommitAsyncHolderErasedMeanwhileStillCommitsTheClaimant()
    {
        var holder = NewUser(statusId: SeedIds.UserStatusTypes.Pending);
        erasureStore.LockHouseholdAsync(holder.Id, Arg.Any<CancellationToken>()).Returns(false);

        var committed = await sut.TryCommitAsync(
            NewUser(),
            holder,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        committed.Should().BeTrue();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
