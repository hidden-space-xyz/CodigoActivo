using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
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

public sealed class UpdateUserCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateUserCommandHandler sut;

    public UpdateUserCommandHandlerTests()
    {
        sut = new UpdateUserCommandHandler(
            users,
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

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new UpdateUserRequest(
            "First",
            "Last",
            "a@test.com",
            "555",
            AdultDob,
            Gender.Female,
            null
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultWithParentIdReturnsBadRequest()
    {
        users.FindReturns(NewUser());
        var request = new UpdateUserRequest(
            "F",
            "L",
            "a@test.com",
            "555",
            AdultDob,
            Gender.Female,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserParentNotAllowedForAdult);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData(null, "555")]
    [InlineData("a@test.com", "   ")]
    public async Task HandleAsyncAdultMissingContactInfoReturnsBadRequest(
        string? email,
        string? phone
    )
    {
        users.FindReturns(NewUser());
        var request = new UpdateUserRequest("F", "L", email, phone, AdultDob, Gender.Female, null);

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserContactInfoRequired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultEmailAlreadyInUseReturnsConflict()
    {
        users.FindReturns(NewUser());
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var request = new UpdateUserRequest(
            "F",
            "L",
            "dup@test.com",
            "555",
            AdultDob,
            Gender.Female,
            null
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserEmailAlreadyInUse);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultPhoneAlreadyInUseReturnsConflict()
    {
        users.FindReturns(NewUser());
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        users
            .PhoneExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var request = new UpdateUserRequest(
            "F",
            "L",
            "a@test.com",
            "555",
            AdultDob,
            Gender.Female,
            null
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserPhoneAlreadyInUse);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidAdultUpdateNormalizesContactPersistsAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, parentId: Guid.NewGuid());
        users.FindReturns(user);
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        users
            .PhoneExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        users.HasUsers(user);
        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateUserRequest(
            "  New  ",
            "  Name  ",
            "  NEW@test.com  ",
            "  999  ",
            AdultDob,
            Gender.Female,
            null
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(id, request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("New");
        result.Value.Email.Should().Be("new@test.com");
        result.Value.Type.Should().NotBeNull();
        result.Value.Type.Name.Should().Be("Socio");
        result.Value.DependentCount.Should().Be(0);
        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Name");
        user.Email.Should().Be("new@test.com");
        user.Phone.Should().Be("999");
        user.Gender.Should().Be(Gender.Female);
        user.ParentId.Should().BeNull();
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
    public async Task HandleAsyncMinorWithoutParentIdReturnsBadRequest()
    {
        users.FindReturns(NewUser());
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, Gender.Male, null);

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserParentIdRequired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncMinorSetAsOwnParentReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        users.FindReturns(NewUser(id: id));
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, Gender.Male, id);

        var result = await sut.HandleAsync(
            new UpdateUserCommand(id, request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCannotBeOwnParent);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncMinorParentMissingReturnsNotFound()
    {
        users.FindReturns(NewUser(), null);
        var request = new UpdateUserRequest(
            "F",
            "L",
            null,
            null,
            MinorDob,
            Gender.Male,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.ParentUserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncMinorParentIsMinorReturnsBadRequest()
    {
        users.FindReturns(NewUser(), NewUser(dob: MinorDob));
        var request = new UpdateUserRequest(
            "F",
            "L",
            null,
            null,
            MinorDob,
            Gender.Male,
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserParentIsMinor);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidMinorUpdateClearsContactAndCredentialsAndSetsParent()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com", phone: "111");
        user.PasswordHash = "hash";
        user.OtpCodeHash = "ABCDEF";
        user.OtpExpiresAt = clock.UtcNow.AddMinutes(10);
        users.FindReturns(user, NewUser(id: parentId));
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            "ignored@test.com",
            "222",
            MinorDob,
            Gender.Male,
            parentId
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(id, request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.ParentId.Should().Be(parentId);
        user.ParentId.Should().Be(parentId);
        user.Email.Should().BeNull();
        user.Phone.Should().BeNull();
        user.PasswordHash.Should().BeNull();
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncMinorReassignedToDifferentParentReturnsForbidden()
    {
        var id = Guid.NewGuid();
        var currentParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        users.FindReturns(NewUser(id: id, parentId: currentParentId), NewUser());
        var request = new UpdateUserRequest(
            "F",
            "L",
            null,
            null,
            MinorDob,
            Gender.Male,
            newParentId
        );

        var result = await sut.HandleAsync(
            new UpdateUserCommand(id, request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserParentReassignmentForbidden);
        await AssertNotSavedAsync();
    }
}
