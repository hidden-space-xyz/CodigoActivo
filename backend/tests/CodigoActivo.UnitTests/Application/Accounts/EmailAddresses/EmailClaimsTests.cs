using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.EmailAddresses;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.EmailAddresses;

public sealed class EmailClaimsTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly EmailClaims sut;

    public EmailClaimsTests()
    {
        sut = new EmailClaims(
            users.LocksEveryAccount(),
            AccountErasers.Create(users, erasureStore, uow),
            uow
        );
    }

    [Fact]
    public async Task TryCommitAsyncHolderThatOwnsTheEmailThrowsWithoutCommitting()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Active);

        var act = () =>
            sut.TryCommitAsync(NewUser(), holder, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TryCommitAsyncHolderVerifiedMeanwhileRefusesTheClaimAndDiscardsIt()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Pending);
        users.ReloadsAs(holder, new { Status = UserStatus.Active });

        var committed = await sut.TryCommitAsync(
            NewUser(),
            holder,
            TestContext.Current.CancellationToken
        );

        committed.Should().BeFalse();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        uow.Received(1).DiscardChanges();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TryCommitAsyncHolderMovedToAnotherAddressMeanwhileCommitsOnlyTheClaimant()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Pending);
        users.ReloadsAs(holder, new { Email = EmailAddress.FromStored("otra@test.com") });

        var committed = await sut.TryCommitAsync(
            NewUser(),
            holder,
            TestContext.Current.CancellationToken
        );

        committed.Should().BeTrue();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryCommitAsyncHolderStillUnverifiedIsErasedWithTheClaim()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Pending);

        var committed = await sut.TryCommitAsync(
            NewUser(),
            holder,
            TestContext.Current.CancellationToken
        );

        committed.Should().BeTrue();
        await users.Received(1).LockAsync(holder, Arg.Any<CancellationToken>());
        users.Received(1).Remove(holder);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryCommitAsyncHolderErasedMeanwhileStillCommitsTheClaimant()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Pending);
        erasureStore.LockHouseholdAsync(holder.Id, Arg.Any<CancellationToken>()).Returns(false);

        var committed = await sut.TryCommitAsync(
            NewUser(),
            holder,
            TestContext.Current.CancellationToken
        );

        committed.Should().BeTrue();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
