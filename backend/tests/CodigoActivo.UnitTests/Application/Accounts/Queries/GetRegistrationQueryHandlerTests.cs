using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Queries;

public sealed class GetRegistrationQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetRegistrationQueryHandler sut;

    public GetRegistrationQueryHandlerTests()
    {
        sut = new GetRegistrationQueryHandler(store, new FakeQueryExecutor());
    }

    private Task<Result<RegisterResponse>> QueryAsync(Guid adultId)
    {
        return sut.HandleAsync(
            new GetRegistrationQuery(adultId),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncAdultMissingReturnsNotFound()
    {
        store.Users.Add(NewUserRow(first: "Otra"));

        var result = await QueryAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncAdultWithMinorsReturnsOnlyTheirMinorsOrderedByFirstName()
    {
        var adult = NewUserRow(first: "Marta", statusName: "Pendiente");
        var stranger = NewUserRow(first: "Luis");
        store.Users.AddRange([
            NewUserRow(first: "Zoe", parent: adult),
            stranger,
            adult,
            NewUserRow(first: "Bruno", parent: stranger),
            NewUserRow(first: "Ana", parent: adult),
        ]);

        var result = await QueryAsync(adult.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Adult.Id.Should().Be(adult.Id);
        result.Value.Adult.FirstName.Should().Be("Marta");
        result.Value.Adult.Status.Name.Should().Be("Pendiente");
        result.Value.Minors.Select(minor => minor.FirstName).Should().Equal("Ana", "Zoe");
        result.Value.Minors.Should().OnlyContain(minor => minor.ParentId == adult.Id);
    }

    [Fact]
    public async Task HandleAsyncAdultWithoutMinorsReturnsNoMinors()
    {
        var adult = NewUserRow(first: "Marta");
        store.Users.Add(adult);

        var result = await QueryAsync(adult.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Adult.Id.Should().Be(adult.Id);
        result.Value.Minors.Should().BeEmpty();
    }
}
