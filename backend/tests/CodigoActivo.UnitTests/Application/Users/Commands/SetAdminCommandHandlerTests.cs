using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class SetAdminCommandHandlerTests
{
    private const string ActingPassword = "acting-admin-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly User actingAdmin;
    private readonly SetAdminCommandHandler sut;

    public SetAdminCommandHandlerTests()
    {
        actingAdmin = NewUser(isAdmin: true);
        actingAdmin.PasswordHash = hasher.Hash(ActingPassword);
        sut = new SetAdminCommandHandler(
            users,
            PasswordGuards.Create(hasher, uow, clock),
            clock,
            uow,
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new ApplicationOptions(),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            NullLogger<SetAdminCommandHandler>.Instance
        );
    }

    private Task<Result> HandleAsync(Guid userId, bool isAdmin, string? currentPassword = null)
    {
        return sut.HandleAsync(
            new SetAdminCommand(
                userId,
                actingAdmin.Id,
                new SetAdminRequest(isAdmin, currentPassword)
            ),
            TestContext.Current.CancellationToken
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
        users.FindReturns(actingAdmin, null);

        var result = await HandleAsync(Guid.NewGuid(), true, ActingPassword);

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantWithCorrectPasswordGrantsAndSaves()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(actingAdmin, user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await HandleAsync(user.Id, true, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeTrue();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email);
        message.TextBody.Should().NotContain(ActingPassword);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task HandleAsyncGrantWithoutPasswordReturnsBadRequest(string? currentPassword)
    {
        var result = await HandleAsync(Guid.NewGuid(), true, currentPassword);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        await users
            .DidNotReceiveWithAnyArgs()
            .FindAsync(default!, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantWithIncorrectPasswordReturnsBadRequest()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id, true, "wrong-password");

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.IsAdmin.Should().BeFalse();
        actingAdmin.PasswordFailedAttempts.Should().Be(1);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantActingUserWithoutPasswordReturnsBadRequest()
    {
        actingAdmin.PasswordHash = null;
        var user = NewUser(isAdmin: false);
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id, true, ActingPassword);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.IsAdmin.Should().BeFalse();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncGrantActingUserMissingReturnsBadRequest()
    {
        var user = NewUser(isAdmin: false);
        users.FindReturns(null, user);

        var result = await HandleAsync(user.Id, true, ActingPassword);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.IsAdmin.Should().BeFalse();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncFlagUnchangedIsNoopAndDoesNotSave()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(actingAdmin, user);

        var result = await HandleAsync(user.Id, true, ActingPassword);

        result.IsSuccess.Should().BeTrue();
        await AssertNotSavedAsync();
        emailSender
            .Sent.Should()
            .BeEmpty("an unchanged flag is not a security event worth reporting");
    }

    [Fact]
    public async Task HandleAsyncRevokeWithOtherAdminsRemainingRevokesWithoutPassword()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);
        users
            .CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(2);

        var result = await HandleAsync(user.Id, false);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeFalse();
        await users
            .Received(1)
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be(user.Email);
    }

    [Fact]
    public async Task HandleAsyncRevokeLastAdminReturnsForbidden()
    {
        var user = NewUser(isAdmin: true);
        users.FindReturns(user);
        users
            .CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await HandleAsync(user.Id, false);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserCannotRemoveLastAdmin);
        user.IsAdmin.Should().BeTrue();
        await AssertNotSavedAsync();
    }
}
