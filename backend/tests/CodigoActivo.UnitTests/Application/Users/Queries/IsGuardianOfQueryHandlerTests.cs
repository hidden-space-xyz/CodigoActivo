using AwesomeAssertions;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class IsGuardianOfQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly IsGuardianOfQueryHandler sut;

    public IsGuardianOfQueryHandlerTests()
    {
        sut = new IsGuardianOfQueryHandler(store, new FakeQueryExecutor());
    }

    private Task<bool> IsGuardianAsync(Guid guardianId, Guid userId)
    {
        return sut.HandleAsync(
            new IsGuardianOfQuery(guardianId, userId),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncUserIsDependentOfGuardianReturnsTrue()
    {
        var guardian = NewUserRow(first: "Marta");
        var child = NewUserRow(first: "Mateo", parent: guardian);
        store.Users.AddRange([guardian, child]);

        var isGuardian = await IsGuardianAsync(guardian.Id, child.Id);

        isGuardian.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncUserIsDependentOfAnotherGuardianReturnsFalse()
    {
        var guardian = NewUserRow(first: "Marta");
        var otherGuardian = NewUserRow(first: "Luis");
        var child = NewUserRow(first: "Mateo", parent: otherGuardian);
        store.Users.AddRange([guardian, otherGuardian, child]);

        var isGuardian = await IsGuardianAsync(guardian.Id, child.Id);

        isGuardian.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncGuardianOfAnotherUserReturnsFalse()
    {
        var guardian = NewUserRow(first: "Marta");
        var child = NewUserRow(first: "Mateo", parent: guardian);
        var stranger = NewUserRow(first: "Luis");
        store.Users.AddRange([guardian, child, stranger]);

        var isGuardian = await IsGuardianAsync(guardian.Id, stranger.Id);

        isGuardian.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsFalse()
    {
        var guardian = NewUserRow(first: "Marta");
        store.Users.AddRange([guardian, NewUserRow(first: "Mateo", parent: guardian)]);

        var isGuardian = await IsGuardianAsync(guardian.Id, Guid.NewGuid());

        isGuardian.Should().BeFalse();
    }
}
