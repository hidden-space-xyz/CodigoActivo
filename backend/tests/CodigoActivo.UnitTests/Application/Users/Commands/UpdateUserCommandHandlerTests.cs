using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class UpdateUserCommandHandlerTests
{
    private const string ActingPassword = "acting-user-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly RecordingLogger<UpdateUserCommandHandler> logger = new();
    private readonly RecordingLogger<AccountSecurityNotifier> notifierLogger = new();
    private readonly User actingUser;
    private readonly UpdateUserCommandHandler sut;

    public UpdateUserCommandHandlerTests()
    {
        actingUser = NewUser();
        actingUser.PasswordHash = hasher.Hash(ActingPassword);
        sut = new UpdateUserCommandHandler(
            users,
            hasher,
            clock,
            uow,
            cacheInvalidator,
            new GetUserByIdQueryHandler(users, new FakeQueryExecutor()),
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new ApplicationOptions(),
                notifierLogger
            ),
            logger
        );
    }

    private void AssertIdentifierChangeLogged(Guid userId)
    {
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Be($"Login identifiers changed by user {actingUser.Id} for user {userId}");
    }

    private Task<Result<UserResponse>> HandleAsync(Guid userId, UpdateUserRequest request)
    {
        return sut.HandleAsync(
            new UpdateUserCommand(userId, actingUser.Id, request),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<User?> AssertActingUserNotLoadedAsync()
    {
        return users
            .Received(1)
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>());
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
            null,
            null
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

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
            Guid.NewGuid(),
            null
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

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
        var request = new UpdateUserRequest(
            "F",
            "L",
            email,
            phone,
            AdultDob,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserContactInfoRequired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultEmailAlreadyInUseReturnsConflict()
    {
        users.FindReturns(NewUser(), actingUser);
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
            null,
            ActingPassword
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserEmailAlreadyInUse);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultPhoneAlreadyInUseReturnsConflict()
    {
        users.FindReturns(NewUser(), actingUser);
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
            null,
            ActingPassword
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserPhoneAlreadyInUse);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidAdultUpdateNormalizesContactPersistsAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, parentId: Guid.NewGuid());
        users.FindReturns(user, actingUser);
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
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

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
    public async Task HandleAsyncChangedIdentifiersWarnsThePreviousAddressWithTheNewOneMasked()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com");
        users.FindReturns(user, actingUser);
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        users
            .PhoneExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "brandnew@test.com",
            "555-0100",
            AdultDob,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("old@test.com");
        message.TextBody.Should().Contain("b***@test.com").And.NotContain("brandnew@test.com");
        AssertIdentifierChangeLogged(id);
    }

    [Fact]
    public async Task HandleAsyncOnlyThePhoneChangedWarnsWithoutQuotingTheUnchangedEmail()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user, actingUser);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0199",
            AdultDob,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.com");
        message.TextBody.Should().NotContain("a***@test.com");
        AssertIdentifierChangeLogged(id);
    }

    [Fact]
    public async Task HandleAsyncAccountGainingIdentifiersLogsTheChangeWithoutNotifying()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: null, phone: null);
        users.FindReturns(user, actingUser);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0100",
            AdultDob,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be("ana@test.com");
        emailSender.Sent.Should().BeEmpty();
        AssertIdentifierChangeLogged(id);
    }

    [Fact]
    public async Task HandleAsyncIdentifierNoticeRefusedByTheLimiterIsLoggedWithoutFailingTheUpdate()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com");
        users.FindReturns(user, actingUser);
        users.HasUsers(user);
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "brandnew@test.com",
            "555-0100",
            AdultDob,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        notifierLogger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                $"Security notification IdentifiersChanged for user {id} was dropped by the email limiter"
            );
    }

    [Fact]
    public async Task HandleAsyncAdultUnchangedContactSavesWithoutPassword()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "  ANA@test.com  ",
            "  555-0100  ",
            AdultDob,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be("ana@test.com");
        user.Phone.Should().Be("555-0100");
        await AssertActingUserNotLoadedAsync();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-password")]
    public async Task HandleAsyncAdultChangedEmailWithoutValidPasswordReturnsBadRequest(
        string? currentPassword
    )
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "attacker@test.com",
            "555-0100",
            AdultDob,
            Gender.Female,
            null,
            currentPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.Email.Should().Be("ana@test.com");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultChangedPhoneWithoutPasswordReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0199",
            AdultDob,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.Phone.Should().Be("555-0100");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultChangedEmailWithActingUserWithoutPasswordReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        actingUser.PasswordHash = null;
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "other@test.com",
            "555-0100",
            AdultDob,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.Email.Should().Be("ana@test.com");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncMinorWithoutParentIdReturnsBadRequest()
    {
        users.FindReturns(NewUser());
        var request = new UpdateUserRequest(
            "F",
            "L",
            null,
            null,
            MinorDob,
            Gender.Male,
            null,
            null
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserParentIdRequired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncMinorSetAsOwnParentReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        users.FindReturns(NewUser(id: id));
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, Gender.Male, id, null);

        var result = await HandleAsync(id, request);

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
            Guid.NewGuid(),
            null
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

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
            Guid.NewGuid(),
            null
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

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
        users.FindReturns(user, NewUser(id: parentId), actingUser);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            "ignored@test.com",
            "222",
            MinorDob,
            Gender.Male,
            parentId,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

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
    public async Task HandleAsyncMinorLosingContactWithoutPasswordReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com", phone: "111");
        users.FindReturns(user, NewUser(id: parentId), actingUser);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            null,
            null,
            MinorDob,
            Gender.Male,
            parentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.Email.Should().Be("old@test.com");
        user.Phone.Should().Be("111");
        user.ParentId.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncMinorWithoutContactOrCredentialsSavesWithoutPassword()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: MinorDob);
        users.FindReturns(user, NewUser(id: parentId));
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            null,
            null,
            MinorDob,
            Gender.Male,
            parentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("Kid");
        user.ParentId.Should().Be(parentId);
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
            newParentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserParentReassignmentForbidden);
        await AssertNotSavedAsync();
    }
}
