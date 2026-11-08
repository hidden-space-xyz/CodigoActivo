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
        sut = new EmailClaims(AccountErasers.Create(users, erasureStore, uow), uow);
    }

    [Fact]
    public async Task TryCommitAsyncHolderThatOwnsTheEmailThrowsWithoutCommitting()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Active);

        var act = () => sut.TryCommitAsync(holder, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TryCommitAsyncEmailTakenByAnotherAccountFirstRefusesTheClaimAndDiscardsIt()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Pending);
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(User),
                    new InvalidOperationException("duplicate")
                )
            );

        var committed = await sut.TryCommitAsync(holder, TestContext.Current.CancellationToken);

        committed.Should().BeFalse();
        uow.Received(1).DiscardChanges();
    }

    [Fact]
    public async Task TryCommitAsyncUnverifiedHolderIsErasedWithTheClaim()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Pending);

        var committed = await sut.TryCommitAsync(holder, TestContext.Current.CancellationToken);

        committed.Should().BeTrue();
        users.Received(1).Remove(holder);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryCommitAsyncHolderErasedMeanwhileStillCommitsTheClaimant()
    {
        var holder = NewUser(statusId: KnownIds.UserStatusTypes.Pending);
        erasureStore.LockAccountAsync(holder.Id, Arg.Any<CancellationToken>()).Returns(false);

        var committed = await sut.TryCommitAsync(holder, TestContext.Current.CancellationToken);

        committed.Should().BeTrue();
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
