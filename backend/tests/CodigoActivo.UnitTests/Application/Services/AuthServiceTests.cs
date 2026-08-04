using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Security;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class AuthServiceTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly PasswordResetOptions passwordReset = new();
    private readonly ApplicationOptions application = new() { BaseUrl = "https://app.test" };
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly AuthService sut;

    public AuthServiceTests()
    {
        sut = new AuthService(
            users,
            uow,
            clock,
            new FakePasswordHasher(),
            emailSender,
            verification,
            passwordReset,
            application,
            NullLogger<AuthService>.Instance,
            cacheInvalidator
        );
    }

    private async Task<List<User>> CaptureAddedUsersAsync()
    {
        var added = new List<User>();
        await users.AddAsync(Arg.Do<User>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    private static readonly DateOnly AdultBirthDate = new(1990, 1, 1);
    private static readonly DateOnly MinorBirthDate = new(2020, 1, 1);

    private static User NewUser(
        Guid? id = null,
        string? email = "ana@test.com",
        string? passwordHash = "fake:password123",
        Guid? statusId = null,
        string? otpCodeHash = null,
        DateTimeOffset? otpExpiresAt = null,
        DateTimeOffset? otpLastSentAt = null
    )
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Ruiz",
            Email = email,
            Phone = "+34123456789",
            PasswordHash = passwordHash,
            BirthDate = AdultBirthDate,
            UserStatusTypeId = statusId ?? SeedIds.UserStatusTypes.Active,
            OtpCodeHash = otpCodeHash,
            OtpExpiresAt = otpExpiresAt,
            OtpLastSentAt = otpLastSentAt,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
    }

    private static RegisterRequest NewRegister(
        string email = "ana@test.com",
        string phone = "+34123456789",
        string password = "password123",
        DateOnly? birthDate = null,
        Gender gender = Gender.Female,
        IReadOnlyList<RegisterMinorRequest>? minors = null
    )
    {
        return new(
            "  Ana  ",
            "  Ruiz  ",
            email,
            phone,
            password,
            birthDate ?? AdultBirthDate,
            gender,
            minors
        );
    }

    private static RegisterMinorRequest NewMinor(
        DateOnly? birthDate = null,
        Gender gender = Gender.Other
    )
    {
        return new("  Leo  ", "  Ruiz  ", birthDate ?? MinorBirthDate, gender);
    }

    private void ExistsReturns(params bool[] seq)
    {
        users
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(seq[0], [.. seq.Skip(1)]);
    }

    private User FindReturns(User? user)
    {
        users.Finds(user);
        return user!;
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static bool IsRegisteredFirstAdmin(
        User? user,
        DateTimeOffset now,
        TimeSpan otpLifetime
    )
    {
        if (user is null)
        {
            return false;
        }

        var hasIdentity =
            string.Equals(user.FirstName, "Ana", StringComparison.Ordinal)
            && string.Equals(user.LastName, "Ruiz", StringComparison.Ordinal)
            && string.Equals(user.Email, "ana@test.com", StringComparison.Ordinal)
            && string.Equals(user.Phone, "+34123456789", StringComparison.Ordinal);
        var hasCredentials =
            string.Equals(user.PasswordHash, "fake:password123", StringComparison.Ordinal)
            && user.OtpCodeHash is not null
            && user.OtpCodeHash.StartsWith(FakePasswordHasher.Prefix, StringComparison.Ordinal);
        var hasCatalog =
            user.IsAdmin
            && user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending
            && user.UserTypeId == SeedIds.UserTypes.Participant;
        var hasSchedule =
            user.OtpExpiresAt == now + otpLifetime
            && user.OtpLastSentAt == now
            && user.CreatedAt == now;

        return hasIdentity && hasCredentials && hasCatalog && hasSchedule;
    }

    private static bool IsPendingParticipantAdult(User? user)
    {
        if (user is null)
        {
            return false;
        }

        var isPendingNonAdmin =
            user.UserStatusTypeId == SeedIds.UserStatusTypes.Pending && !user.IsAdmin;

        return isPendingNonAdmin
            && user.Gender is Gender.Female
            && user.UserTypeId == SeedIds.UserTypes.Participant;
    }

    private static bool IsDependentParticipantMinor(User? user)
    {
        if (user is null)
        {
            return false;
        }

        var isDependentLeo =
            user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent && string.Equals(user.FirstName, "Leo", StringComparison.Ordinal);

        return isDependentLeo
            && user.Gender is Gender.Other
            && user.ParentId is not null
            && user.UserTypeId == SeedIds.UserTypes.Participant;
    }

    private static User NewPendingWithOtp(
        TestClock clock,
        string code = "the-otp-code",
        DateTimeOffset? otpLastSentAt = null
    )
    {
        return NewUser(
            statusId: SeedIds.UserStatusTypes.Pending,
            otpCodeHash: FakePasswordHasher.Prefix + code,
            otpExpiresAt: clock.UtcNow.AddMinutes(5),
            otpLastSentAt: otpLastSentAt ?? clock.UtcNow.AddMinutes(-10)
        );
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsUnauthorized()
    {
        User? missing = null;
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await sut.LoginAsync(
            new LoginRequest("nobody@test.com", "password123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task LoginAsync_PasswordHashNotSet_ReturnsUnauthorized(string? hash)
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(passwordHash: hash));

        var result = await sut.LoginAsync(
            new LoginRequest("ana@test.com", "password123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task LoginAsync_PasswordDoesNotVerify_ReturnsUnauthorized()
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(passwordHash: "fake:correct"));

        var result = await sut.LoginAsync(
            new LoginRequest("ana@test.com", "wrong"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.InvalidCredentials);
        await AssertNotSavedAsync();
    }

    [Theory]
    [MemberData(nameof(BlockedStatuses))]
    public async Task LoginAsync_NonActiveStatus_ReturnsForbidden(Guid statusId, ErrorCode expected)
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(statusId: statusId));

        var result = await sut.LoginAsync(
            new LoginRequest("ana@test.com", "password123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Forbidden, expected);
        await AssertNotSavedAsync();
    }

    public static TheoryData<Guid, ErrorCode> BlockedStatuses()
    {
        return new()
        {
            { SeedIds.UserStatusTypes.Blocked, ErrorCode.UserAccountBlocked },
            { SeedIds.UserStatusTypes.Dependent, ErrorCode.UserAccountIsDependent },
            { SeedIds.UserStatusTypes.Pending, ErrorCode.UserAccountPendingVerification },
        };
    }

    [Fact]
    public async Task LoginAsync_PendingUserVerificationNotRequired_ActivatesUser()
    {
        verification.Required = false;
        var user = NewUser(statusId: SeedIds.UserStatusTypes.Pending, otpCodeHash: "ABCDEF");
        users.GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id, statusId: SeedIds.UserStatusTypes.Active));

        var result = await sut.LoginAsync(
            new LoginRequest("ana@test.com", "password123"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.OtpCodeHash.Should().BeNull();
        result.Value.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_TrimsIdentifierAndRecordsLogin()
    {
        var user = NewUser();
        users.GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        var result = await sut.LoginAsync(
            new LoginRequest("  ana@test.com  ", "password123"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("ana@test.com");
        result.Value.Type.Should().BeNull();
        user.LastLoginAt.Should().NotBeNull();
        await users
            .Received(1)
            .GetByEmailOrPhoneAsync("ana@test.com", Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentAsync_UserMissing_ReturnsUnauthorized()
    {
        User? missing = null;
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(missing);

        var result = await sut.GetCurrentAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Unauthorized, ErrorCode.CurrentUserNotFound);
    }

    [Fact]
    public async Task RegisterAsync_AdultBirthDateIsMinor_ReturnsBadRequest()
    {
        var result = await sut.RegisterAsync(
            NewRegister(birthDate: MinorBirthDate),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.RegisterAdultCannotBeMinor);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData("   ", "+34123456789", "password123")]
    [InlineData("ana@test.com", "   ", "password123")]
    [InlineData("ana@test.com", "+34123456789", "   ")]
    public async Task RegisterAsync_MissingContactInfo_ReturnsBadRequest(
        string email,
        string phone,
        string password
    )
    {
        ExistsReturns(false);

        var result = await sut.RegisterAsync(
            NewRegister(email: email, phone: phone, password: password),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.RegisterContactInfoRequired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task RegisterAsync_EmailOrPhoneInUse_ReturnsConflict()
    {
        ExistsReturns(false, true);

        var result = await sut.RegisterAsync(NewRegister(), TestContext.Current.CancellationToken);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.RegisterEmailOrPhoneAlreadyInUse);
        await AssertNotSavedAsync();
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task RegisterAsync_MinorWithAdultBirthDate_ReturnsBadRequest()
    {
        ExistsReturns(false, false);

        var result = await sut.RegisterAsync(
            NewRegister(minors: [NewMinor(birthDate: AdultBirthDate)]),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.RegisterMinorBirthDateNotMinor);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task RegisterAsync_FirstUser_BecomesAdminWithParticipantType()
    {
        clock.UtcNow = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await sut.RegisterAsync(NewRegister(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Minors.Should().BeEmpty();
        result.Value.RequiresVerification.Should().BeTrue();

        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u =>
                    IsRegisteredFirstAdmin(u, clock.UtcNow, verification.OtpLifetime)
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_NewAdult_SendsGuidOtpHashedAtRestAndInvalidatesCache()
    {
        var added = await CaptureAddedUsersAsync();
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await sut.RegisterAsync(NewRegister(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().HaveCount(1);
        var code = emailSender.LastCode();
        Guid.TryParse(code, out _).Should().BeTrue("the OTP is now a GUID");

        var email = emailSender.Sent[0];
        email.ToAddress.Should().Be("ana@test.com");
        email.ToName.Should().Be("Ana");
        email.TextBody.Should().Contain(code);
        email.Subject.Should().NotContain(code);
        email.TextBody.Should().Contain("https://app.test/verify-account?userId=");
        email.HtmlBody.Should().Contain("/verify-account?userId=");

        added.Should().ContainSingle();
        added[0].OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Users)
                )
            );
    }

    [Fact]
    public async Task RegisterAsync_EmailSendFails_SucceedsAndClearsLastSent()
    {
        var added = await CaptureAddedUsersAsync();
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await sut.RegisterAsync(NewRegister(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresVerification.Should().BeTrue();
        added.Should().ContainSingle();
        added[0].OtpLastSentAt.Should().BeNull();
        added[0]
            .OtpCodeHash.Should()
            .NotBeNull("the code is still issued so a resend can replace it");
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_EmailSendCancelled_PropagatesOperationCanceledException()
    {
        var added = await CaptureAddedUsersAsync();
        emailSender.ThrowOnSend = new OperationCanceledException("registration cancelled");
        ExistsReturns(false, false);

        var act = () => sut.RegisterAsync(NewRegister(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
        added.Should().ContainSingle();
        added[0].OtpLastSentAt.Should().Be(clock.UtcNow, "the swallow-and-clear catch is skipped");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_SubsequentUserWithMinor_CreatesAdultAndMinorAsParticipants()
    {
        clock.UtcNow = new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero);
        ExistsReturns(true, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([NewUser(email: null)]);

        var result = await sut.RegisterAsync(
            NewRegister(minors: [NewMinor()]),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Minors.Should().HaveCount(1);

        await users.Received(2).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => IsPendingParticipantAdult(u)),
                Arg.Any<CancellationToken>()
            );
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => IsDependentParticipantMinor(u)),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);

        var result = await sut.VerifyAsync(
            Guid.NewGuid(),
            "123456",
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task VerifyAsync_UserNotPending_ReturnsBadRequest()
    {
        var user = FindReturns(
            NewUser(
                statusId: SeedIds.UserStatusTypes.Active,
                otpCodeHash: FakePasswordHasher.Prefix + "123456",
                otpExpiresAt: clock.UtcNow.AddMinutes(5)
            )
        );

        var result = await sut.VerifyAsync(
            user.Id,
            "123456",
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.OtpInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    public static TheoryData<string, bool, int?> InvalidOtpCases()
    {
        return new()
        {
            { "   ", true, 5 },
            { "123456", false, 5 },
            { "123456", true, null },
            { "123456", true, -5 },
        };
    }

    [Theory]
    [MemberData(nameof(InvalidOtpCases))]
    public async Task VerifyAsync_InvalidOrExpiredOtp_ReturnsBadRequest(
        string otpArgument,
        bool hasStoredHash,
        int? expiresInMinutes
    )
    {
        var user = FindReturns(
            NewUser(
                statusId: SeedIds.UserStatusTypes.Pending,
                otpCodeHash: hasStoredHash ? FakePasswordHasher.Prefix + "123456" : null,
                otpExpiresAt: expiresInMinutes is null
                    ? null
                    : clock.UtcNow.AddMinutes(expiresInMinutes.Value)
            )
        );

        var result = await sut.VerifyAsync(
            user.Id,
            otpArgument,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.OtpInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task VerifyAsync_WrongCode_ReturnsBadRequestWithoutPersisting()
    {
        var user = FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));

        var result = await sut.VerifyAsync(
            user.Id,
            "a-wrong-code",
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.OtpInvalidOrExpired);
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task VerifyAsync_CorrectCode_ActivatesUserAndClearsOtp()
    {
        var user = FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id));

        var result = await sut.VerifyAsync(
            user.Id,
            "  the-real-code  ",
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);

        var result = await sut.ResendVerificationAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_UserNotPending_ReturnsConflict()
    {
        var user = FindReturns(NewUser(statusId: SeedIds.UserStatusTypes.Active));

        var result = await sut.ResendVerificationAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_PendingUserWithoutEmail_ReturnsConflict()
    {
        var user = FindReturns(NewPendingWithOtp(clock));
        user.Email = null;

        var result = await sut.ResendVerificationAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_NeverSentBefore_AllowsImmediateResend()
    {
        var user = FindReturns(
            NewUser(
                statusId: SeedIds.UserStatusTypes.Pending,
                otpCodeHash: null,
                otpExpiresAt: null,
                otpLastSentAt: null
            )
        );

        var result = await sut.ResendVerificationAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + emailSender.LastCode());
        user.OtpLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationAsync_VerificationNotRequired_ReturnsConflict()
    {
        verification.Required = false;
        var user = FindReturns(NewPendingWithOtp(clock));

        var result = await sut.ResendVerificationAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_WithinCooldown_ReturnsConflict()
    {
        var user = FindReturns(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddSeconds(-10))
        );

        var result = await sut.ResendVerificationAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResendVerificationAsync_CooldownElapsed_IssuesNewCodeAndPersists()
    {
        var user = FindReturns(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.ResendVerificationAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var newCode = emailSender.LastCode();
        Guid.TryParse(newCode, out _).Should().BeTrue("the OTP is now a GUID");
        newCode.Should().NotBe("old-code");
        user.OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + newCode);
        user.OtpExpiresAt.Should().Be(clock.UtcNow + verification.OtpLifetime);
        user.OtpLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationAsync_EmailSendFails_DoesNotPersistNewCode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = FindReturns(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.OtpCodeHash;

        var act = () => sut.ResendVerificationAsync(user.Id, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        user.OtpCodeHash.Should().Be(previousHash);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResendVerificationAsync_QuotaDenied_ReturnsConflictAndKeepsTheIssuedCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = FindReturns(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.OtpCodeHash;
        var previousSentAt = user.OtpLastSentAt;

        var result = await sut.ResendVerificationAsync(
            user.Id,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.OtpResendCooldownActive);
        user.OtpCodeHash.Should().Be(previousHash);
        user.OtpLastSentAt.Should().Be(previousSentAt);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ForgotPasswordAsync_QuotaDenied_StillReportsSuccessAndIssuesNoCode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Global);
        var user = FindReturns(NewUser());

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest(user.Email!),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCodeHash.Should().BeNull();
        user.PasswordResetLastSentAt.Should().BeNull();
        await AssertNotSavedAsync();
    }

    private static User NewUserWithResetCode(
        TestClock clock,
        string code = "the-reset-code",
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? lastSentAt = null
    )
    {
        var user = NewUser();
        user.PasswordResetCodeHash = FakePasswordHasher.Prefix + code;
        user.PasswordResetExpiresAt = expiresAt ?? clock.UtcNow.AddMinutes(5);
        user.PasswordResetLastSentAt = lastSentAt ?? clock.UtcNow.AddMinutes(-10);
        return user;
    }

    [Fact]
    public async Task ForgotPasswordAsync_UserMissing_ReturnsSuccessWithoutSending()
    {
        FindReturns(null);

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("nobody@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ForgotPasswordAsync_UserWithoutPassword_ReturnsSuccessWithoutSending()
    {
        FindReturns(NewUser(passwordHash: null));

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    public static TheoryData<Guid> IneligibleResetStatuses()
    {
        return [SeedIds.UserStatusTypes.Blocked, SeedIds.UserStatusTypes.Dependent];
    }

    [Theory]
    [MemberData(nameof(IneligibleResetStatuses))]
    public async Task ForgotPasswordAsync_IneligibleStatus_ReturnsSuccessWithoutSending(
        Guid statusId
    )
    {
        FindReturns(NewUser(statusId: statusId));

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithinCooldown_ReturnsSuccessWithoutSending()
    {
        var user = FindReturns(NewUser());
        user.PasswordResetLastSentAt = clock.UtcNow.AddSeconds(-10);

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ForgotPasswordAsync_EligibleUser_SendsGuidCodeAndPersistsHash()
    {
        var user = FindReturns(NewUser());

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var code = emailSender.LastCode();
        Guid.TryParse(code, out _).Should().BeTrue("the reset code is a GUID");
        user.PasswordResetCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
        user.PasswordResetExpiresAt.Should().Be(clock.UtcNow + passwordReset.CodeLifetime);
        user.PasswordResetLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        emailSender.Sent[0].ToAddress.Should().Be(user.Email);
        emailSender.Sent[0].TextBody.Should().Contain("/reset-password?userId=");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_CooldownElapsed_ReplacesPreviousCode()
    {
        var user = FindReturns(
            NewUserWithResetCode(clock, code: "old-code", lastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var newCode = emailSender.LastCode();
        newCode.Should().NotBe("old-code");
        user.PasswordResetCodeHash.Should().Be(FakePasswordHasher.Prefix + newCode);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_EmailSendFails_ReturnsSuccessWithoutPersisting()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = FindReturns(NewUser());

        var result = await sut.ForgotPasswordAsync(
            new ForgotPasswordRequest("ana@test.com"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordResetCodeHash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);

        var result = await sut.ResetPasswordAsync(
            Guid.NewGuid(),
            new ResetPasswordRequest("some-code", "newPassword123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_NoCodeRequested_ReturnsBadRequest()
    {
        var user = FindReturns(NewUser());

        var result = await sut.ResetPasswordAsync(
            user.Id,
            new ResetPasswordRequest("some-code", "newPassword123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredCode_ReturnsBadRequest()
    {
        var user = FindReturns(NewUserWithResetCode(clock, expiresAt: clock.UtcNow.AddMinutes(-5)));

        var result = await sut.ResetPasswordAsync(
            user.Id,
            new ResetPasswordRequest("the-reset-code", "newPassword123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_WrongCode_ReturnsBadRequestWithoutPersisting()
    {
        var user = FindReturns(NewUserWithResetCode(clock, code: "the-real-code"));
        var previousPasswordHash = user.PasswordHash;

        var result = await sut.ResetPasswordAsync(
            user.Id,
            new ResetPasswordRequest("a-wrong-code", "newPassword123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        user.PasswordHash.Should().Be(previousPasswordHash);
        user.PasswordResetCodeHash.Should().NotBeNull("a wrong guess must not consume the code");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_UserBlockedAfterRequest_ReturnsBadRequest()
    {
        var user = FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));
        user.UserStatusTypeId = SeedIds.UserStatusTypes.Blocked;

        var result = await sut.ResetPasswordAsync(
            user.Id,
            new ResetPasswordRequest("the-reset-code", "newPassword123"),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.PasswordResetInvalidOrExpired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task ResetPasswordAsync_CorrectCode_ChangesPasswordAndClearsCode()
    {
        var user = FindReturns(NewUserWithResetCode(clock, code: "the-reset-code"));

        var result = await sut.ResetPasswordAsync(
            user.Id,
            new ResetPasswordRequest("  THE-RESET-CODE  ", "newPassword123"),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "newPassword123");
        user.PasswordResetCodeHash.Should().BeNull();
        user.PasswordResetExpiresAt.Should().BeNull();
        user.PasswordResetLastSentAt.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
