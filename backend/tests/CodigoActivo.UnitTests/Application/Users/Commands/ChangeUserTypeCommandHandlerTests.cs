using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class ChangeUserTypeCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakeReadStore readStore = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly ChangeUserTypeCommandHandler sut;

    public ChangeUserTypeCommandHandlerTests()
    {
        sut = new ChangeUserTypeCommandHandler(
            users,
            readStore,
            new FakeQueryExecutor(),
            clock,
            uow,
            cacheInvalidator
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private void TypeExists(Guid typeId, bool exists)
    {
        readStore.UserTypes.Add(
            new UserTypeRow
            {
                Id = exists ? typeId : Guid.NewGuid(),
                Name = "Tipo",
                Color = "#000",
            }
        );
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncRoleMissingReturnsNotFound()
    {
        var roleId = Guid.NewGuid();
        users.FindReturns(NewUser());
        TypeExists(roleId, false);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(Guid.NewGuid(), roleId),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserTypeNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncNewTypeDiffersFromCurrentReplacesTypeSavesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        users.FindReturns(user);
        TypeExists(roleId, true);
        clock.UtcNow = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(id, roleId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.UserTypeId.Should().Be(roleId);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Users)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncTypeUnchangedIsNoopAndDoesNotSave()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        Persisted.Overwrite(user, new { UserTypeId = roleId });
        users.FindReturns(user);
        TypeExists(roleId, true);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(id, roleId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await AssertNotSavedAsync();
    }
}
