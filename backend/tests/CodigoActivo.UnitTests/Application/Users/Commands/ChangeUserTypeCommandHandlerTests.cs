using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class ChangeUserTypeCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserTypeRepository userTypes = Substitute.For<IUserTypeRepository>();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly ChangeUserTypeCommandHandler sut;

    public ChangeUserTypeCommandHandlerTests()
    {
        sut = new ChangeUserTypeCommandHandler(
            users,
            userTypes,
            clock,
            uow,
            cacheInvalidator,
            new GetUserByIdQueryHandler(users, new FakeQueryExecutor())
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private void TypeExists(bool exists)
    {
        userTypes
            .ExistsAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(exists);
    }

    [Fact]
    public async Task HandleAsync_UserMissing_ReturnsNotFound()
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
    public async Task HandleAsync_RoleMissing_ReturnsNotFound()
    {
        users.FindReturns(NewUser());
        TypeExists(false);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserTypeNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsync_NewTypeDiffersFromCurrent_ReplacesTypeSavesAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        users.FindReturns(user);
        TypeExists(true);
        users.HasUsers(NewUser(id: id));
        clock.UtcNow = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(id, roleId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().NotBeNull();
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
    public async Task HandleAsync_TypeUnchanged_IsNoopAndDoesNotSave()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        user.UserTypeId = roleId;
        users.FindReturns(user);
        TypeExists(true);
        users.HasUsers(NewUser(id: id));

        var result = await sut.HandleAsync(
            new ChangeUserTypeCommand(id, roleId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await AssertNotSavedAsync();
    }
}
