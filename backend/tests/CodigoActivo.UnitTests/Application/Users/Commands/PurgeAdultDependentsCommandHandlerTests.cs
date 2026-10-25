using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class PurgeAdultDependentsCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly PurgeAdultDependentsCommandHandler sut;

    public PurgeAdultDependentsCommandHandlerTests()
    {
        sut = new PurgeAdultDependentsCommandHandler(
            users,
            AccountErasers.Create(users, erasureStore, uow),
            uow.RunsTransactions(),
            clock
        );
    }

    private static User Dependent(DateOnly birthDate)
    {
        return NewUser(
            parentId: Guid.NewGuid(),
            dob: birthDate,
            email: null,
            phone: null,
            statusId: KnownIds.UserStatusTypes.Dependent
        );
    }

    private void Lists(params User[] dependents)
    {
        users
            .ListDependentsBornUpToAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(dependents);
    }

    private Task<CodigoActivo.Domain.Common.Result<int>> HandleAsync()
    {
        return sut.HandleAsync(
            new PurgeAdultDependentsCommand(),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncErasesTheDependentsWhoCameOfAgeWithABlockedCopy()
    {
        var turnsEighteenToday = Dependent(clock.Today.AddYears(-18));
        var older = Dependent(clock.Today.AddYears(-19));
        Lists(turnsEighteenToday, older);
        users.LocksEveryAccount();

        var result = await HandleAsync();

        result.Value.Should().Be(2);
        await users
            .Received(1)
            .ListDependentsBornUpToAsync(clock.Today.AddYears(-18), Arg.Any<CancellationToken>());
        users.Received(1).Remove(turnsEighteenToday);
        users.Received(1).Remove(older);
        await erasureStore
            .Received(1)
            .LoadHouseholdAsync(turnsEighteenToday.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncKeepsADependentWhoseBirthDateWasCorrectedSinceItWasListed()
    {
        var dependent = Dependent(clock.Today.AddYears(-18));
        Lists(dependent);
        users.ReloadsAs(dependent, new { BirthDate = clock.Today.AddYears(-17) });

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Fact]
    public async Task HandleAsyncSkipsADependentGoneOnceLocked()
    {
        var dependent = Dependent(clock.Today.AddYears(-18));
        Lists(dependent);
        users.LockAsync(dependent, Arg.Any<CancellationToken>()).Returns(false);

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
    }
}
