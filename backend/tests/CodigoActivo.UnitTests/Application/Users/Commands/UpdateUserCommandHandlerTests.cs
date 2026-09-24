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
using Microsoft.Extensions.Logging;
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
    private readonly RecordingLogger<AccountSecurityNotifier> notifierLogger = new();
    private readonly User actingUser;
    private readonly UpdateUserCommandHandler sut;

    public UpdateUserCommandHandlerTests()
    {
        actingUser = NewUser();
        actingUser.PasswordHash = hasher.Hash(ActingPassword);
        sut = new UpdateUserCommandHandler(
            users,
            PasswordGuards.Create(hasher, uow, clock),
            clock,
            uow,
            cacheInvalidator,
            new GetUserByIdQueryHandler(users, new FakeQueryExecutor()),
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new ApplicationOptions(),
                notifierLogger
            )
        );
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
            null,
            AdultNationalId,
            false,
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
            null,
            AdultNationalId,
            false,
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
            null,
            AdultNationalId,
            false,
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
            null,
            AdultNationalId,
            false,
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
            null,
            AdultNationalId,
            false,
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
        var user = NewUser(id: id);
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
            null,
            AdultNationalId,
            false,
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
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("old@test.com");
        message.TextBody.Should().Contain("b***@test.com").And.NotContain("brandnew@test.com");
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
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.com");
        message.TextBody.Should().NotContain("a***@test.com");
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
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be("ana@test.com");
        emailSender.Sent.Should().BeEmpty();
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
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        var entry = notifierLogger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("A IdentifiersChanged security notification was dropped by the email limiter")
            .And.NotContain(id.ToString());
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
            null,
            AdultNationalId,
            false,
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
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("wrong-password", 1)]
    public async Task HandleAsyncAdultChangedEmailWithoutValidPasswordReturnsBadRequest(
        string? currentPassword,
        int countedFailures
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
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            currentPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserCurrentPasswordIncorrect);
        user.Email.Should().Be("ana@test.com");
        actingUser.PasswordFailedAttempts.Should().Be(countedFailures);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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
            null,
            AdultNationalId,
            false,
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
            null,
            AdultNationalId,
            false,
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
    public async Task HandleAsyncStandaloneAccountGivenABirthDateReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        user.PasswordHash = "hash";
        user.OtpCodeHash = "ABCDEF";
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            "ana@test.com",
            "555-0100",
            AdultDob,
            AdultNationalId,
            false,
            Gender.Male,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserBirthDateNotAllowedForAdult);
        user.Email.Should().Be("ana@test.com");
        user.Phone.Should().Be("555-0100");
        user.PasswordHash.Should().Be("hash");
        user.OtpCodeHash.Should().Be("ABCDEF");
        user.ParentId.Should().BeNull();
        user.BirthDate.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncStandaloneAccountGivenAParentAndABirthDateIsRefusedForTheBirthDate()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id);
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            null,
            null,
            MinorDob,
            null,
            false,
            Gender.Male,
            Guid.NewGuid(),
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserBirthDateNotAllowedForAdult);
        user.ParentId.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData(" - ")]
    public async Task HandleAsyncStandaloneAccountWithoutNationalIdReturnsBadRequest(
        string? nationalId
    )
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id);
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0100",
            null,
            nationalId,
            true,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserNationalIdRequired);
        user.NationalId.Should().Be(AdultNationalId);
        user.PromotionalConsent.Should().BeFalse();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncStandaloneAccountNationalIdOfAnotherUserReturnsConflict()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user, actingUser);
        users.NationalIdExistsAsync("X1234567L", id, Arg.Any<CancellationToken>()).Returns(true);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "other@test.com",
            "555-0100",
            null,
            " x-1234567-l ",
            true,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserNationalIdAlreadyInUse);
        user.NationalId.Should().Be(AdultNationalId);
        user.Email.Should().Be("ana@test.com");
        actingUser.PasswordFailedAttempts.Should().Be(0);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncStandaloneAccountStoresNormalizedNationalIdAndConsentWithoutPassword()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0100",
            null,
            " x-1234567-l ",
            true,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.NationalId.Should().Be("X1234567L");
        user.PromotionalConsent.Should().BeTrue();
        user.BirthDate.Should().BeNull();
        result.Value.NationalId.Should().Be("X1234567L");
        result.Value.PromotionalConsent.Should().BeTrue();
        await users
            .Received(1)
            .NationalIdExistsAsync("X1234567L", id, Arg.Any<CancellationToken>());
        await AssertActingUserNotLoadedAsync();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncStandaloneAccountWithdrawingConsentIsSaved()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        user.PromotionalConsent = true;
        users.FindReturns(user);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.PromotionalConsent.Should().BeFalse();
        user.NationalId.Should().Be(AdultNationalId);
    }

    [Fact]
    public async Task HandleAsyncDependentKeepsItsGuardianContactAndCredentialsWhileStillAMinor()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: MinorDob);
        users.FindReturns(user);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            "ignored@test.com",
            "222",
            MinorDob.AddDays(1),
            AdultNationalId,
            true,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("Kid");
        user.BirthDate.Should().Be(MinorDob.AddDays(1));
        user.Gender.Should().Be(Gender.Female);
        user.ParentId.Should().Be(parentId);
        user.Email.Should()
            .BeNull("a dependent's contact details are never taken from the request");
        user.Phone.Should().BeNull();
        user.NationalId.Should().BeNull("a dependent never stores a DNI or NIE");
        user.PromotionalConsent.Should().BeFalse();
        await users
            .DidNotReceiveWithAnyArgs()
            .NationalIdExistsAsync(default!, default, TestContext.Current.CancellationToken);
        await AssertActingUserNotLoadedAsync();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncDependentRepeatingItsOwnGuardianIsAccepted()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: MinorDob);
        users.FindReturns(user);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            null,
            null,
            MinorDob,
            null,
            false,
            Gender.Male,
            parentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.ParentId.Should().Be(parentId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncDependentReassignedToDifferentParentReturnsForbidden()
    {
        var id = Guid.NewGuid();
        var currentParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: currentParentId, email: null, phone: null);
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "F",
            "L",
            null,
            null,
            MinorDob,
            null,
            false,
            Gender.Male,
            newParentId,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserParentReassignmentForbidden);
        user.ParentId.Should().Be(currentParentId);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncDependentGivenAnAdultBirthDateReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: MinorDob);
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Grown",
            "Doe",
            null,
            null,
            AdultDob,
            null,
            false,
            Gender.Male,
            parentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserChildBirthDateNotMinor);
        user.FirstName.Should().Be("Ana");
        user.BirthDate.Should().Be(MinorDob);
        user.ParentId.Should().Be(parentId);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultDependentKeepingItsStoredBirthDateIsAccepted()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: AdultDob);
        users.FindReturns(user);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Grown",
            "Doe",
            null,
            null,
            AdultDob,
            null,
            false,
            Gender.Female,
            parentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("Grown");
        user.Gender.Should().Be(Gender.Female);
        user.BirthDate.Should().Be(AdultDob);
        user.ParentId.Should().Be(parentId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncAdultDependentGivenAnotherAdultBirthDateReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: AdultDob);
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Grown",
            "Doe",
            null,
            null,
            AdultDob.AddDays(1),
            null,
            false,
            Gender.Male,
            parentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserChildBirthDateNotMinor);
        user.FirstName.Should().Be("Ana");
        user.BirthDate.Should().Be(AdultDob);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultDependentGivenAMinorBirthDateIsAccepted()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: AdultDob);
        users.FindReturns(user);
        users.HasUsers(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            null,
            null,
            MinorDob,
            null,
            false,
            Gender.Male,
            parentId,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.BirthDate.Should().Be(MinorDob);
        user.ParentId.Should().Be(parentId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncDependentWithoutBirthDateReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: MinorDob);
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            null,
            null,
            null,
            AdultNationalId,
            true,
            Gender.Male,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserChildBirthDateRequired);
        user.BirthDate.Should().Be(MinorDob);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultDependentReassignedToDifferentParentReturnsForbidden()
    {
        var id = Guid.NewGuid();
        var currentParentId = Guid.NewGuid();
        var user = NewUser(
            id: id,
            parentId: currentParentId,
            email: null,
            phone: null,
            dob: AdultDob
        );
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Grown",
            "Doe",
            null,
            null,
            null,
            AdultNationalId,
            false,
            Gender.Male,
            Guid.NewGuid(),
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Forbidden, ErrorCode.UserParentReassignmentForbidden);
        user.ParentId.Should().Be(currentParentId);
        await AssertNotSavedAsync();
    }
}
