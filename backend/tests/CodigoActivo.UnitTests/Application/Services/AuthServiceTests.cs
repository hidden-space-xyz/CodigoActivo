using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
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
    private readonly IUserTypeRepository userTypes = Substitute.For<IUserTypeRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly ApplicationOptions application = new() { BaseUrl = "https://app.test" };
    private readonly AuthService sut;

    public AuthServiceTests()
    {
        sut = new AuthService(
            users,
            userTypes,
            uow,
            clock,
            new FakePasswordHasher(),
            emailSender,
            verification,
            application,
            NullLogger<AuthService>.Instance
        );
    }

    private List<User> CaptureAddedUsers()
    {
        var added = new List<User>();
        users.AddAsync(Arg.Do<User>(added.Add), Arg.Any<CancellationToken>());
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
    ) =>
        new()
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

    private static UserType NewUserType(
        Guid? id = null,
        bool hidden = false,
        bool allowedForAdults = true,
        bool allowedForMinors = true
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Role",
            Description = string.Empty,
            Color = "#ffffff",
            Hidden = hidden,
            IsAllowedForAdults = allowedForAdults,
            IsAllowedForMinors = allowedForMinors,
        };

    private static RegisterRequest NewRegister(
        string email = "ana@test.com",
        string phone = "+34123456789",
        string password = "password123",
        DateOnly? birthDate = null,
        Guid? roleId = null,
        IReadOnlyList<RegisterMinorRequest>? minors = null
    ) =>
        new(
            "  Ana  ",
            "  Ruiz  ",
            email,
            phone,
            password,
            birthDate ?? AdultBirthDate,
            roleId ?? SeedIds.UserTypes.Member,
            minors
        );

    private static RegisterMinorRequest NewMinor(Guid? roleId = null, DateOnly? birthDate = null) =>
        new(
            "  Leo  ",
            "  Ruiz  ",
            birthDate ?? MinorBirthDate,
            roleId ?? SeedIds.UserTypes.Participant
        );

    private void ExistsReturns(params bool[] seq) =>
        users
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(seq[0], seq.Skip(1).ToArray());

    private User FindReturns(User? user)
    {
        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(user);
        return user!;
    }

    private static User NewPendingWithOtp(
        TestClock clock,
        string code = "the-otp-code",
        DateTimeOffset? otpLastSentAt = null
    ) =>
        NewUser(
            statusId: SeedIds.UserStatusTypes.Pending,
            otpCodeHash: FakePasswordHasher.Prefix + code,
            otpExpiresAt: clock.UtcNow.AddMinutes(5),
            otpLastSentAt: otpLastSentAt ?? clock.UtcNow.AddMinutes(-10)
        );

    [Fact]
    public async Task LoginAsync_returns_unauthorized_when_user_not_found()
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await sut.LoginAsync(new LoginRequest("nobody@test.com", "password123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
        result.Error.Code.Should().Be(ErrorCode.InvalidCredentials);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task LoginAsync_returns_unauthorized_when_password_hash_not_set(string? hash)
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(passwordHash: hash));

        var result = await sut.LoginAsync(new LoginRequest("ana@test.com", "password123"));

        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
        result.Error.Code.Should().Be(ErrorCode.InvalidCredentials);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task LoginAsync_returns_unauthorized_when_password_does_not_verify()
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(passwordHash: "fake:correct"));

        var result = await sut.LoginAsync(new LoginRequest("ana@test.com", "wrong"));

        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
        result.Error.Code.Should().Be(ErrorCode.InvalidCredentials);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [MemberData(nameof(BlockedStatuses))]
    public async Task LoginAsync_returns_forbidden_for_non_active_status(
        Guid statusId,
        ErrorCode expected
    )
    {
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NewUser(statusId: statusId));

        var result = await sut.LoginAsync(new LoginRequest("ana@test.com", "password123"));

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(expected);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    public static TheoryData<Guid, ErrorCode> BlockedStatuses() =>
        new()
        {
            { SeedIds.UserStatusTypes.Blocked, ErrorCode.UserAccountBlocked },
            { SeedIds.UserStatusTypes.Dependent, ErrorCode.UserAccountIsDependent },
            { SeedIds.UserStatusTypes.Pending, ErrorCode.UserAccountPendingVerification },
        };

    [Fact]
    public async Task LoginAsync_activates_pending_user_when_verification_not_required()
    {
        verification.Required = false;
        var user = NewUser(statusId: SeedIds.UserStatusTypes.Pending, otpCodeHash: "ABCDEF");
        users.GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id, statusId: SeedIds.UserStatusTypes.Active));

        var result = await sut.LoginAsync(new LoginRequest("ana@test.com", "password123"));

        result.IsSuccess.Should().BeTrue();
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.OtpCodeHash.Should().BeNull();
        // The self-healed response must reflect Active, not the stale loaded Pending navigation.
        result.Value.Status.Id.Should().Be(SeedIds.UserStatusTypes.Active);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_succeeds_trims_identifier_records_login_and_maps_response()
    {
        var user = NewUser();
        users.GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        var result = await sut.LoginAsync(new LoginRequest("  ana@test.com  ", "password123"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("ana@test.com");
        user.LastLoginAt.Should().NotBeNull();
        await users
            .Received(1)
            .GetByEmailOrPhoneAsync("ana@test.com", Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentAsync_returns_unauthorized_when_missing()
    {
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await sut.GetCurrentAsync(Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
        result.Error.Code.Should().Be(ErrorCode.CurrentUserNotFound);
    }

    [Fact]
    public async Task GetCurrentAsync_returns_mapped_user_when_found()
    {
        var user = NewUser();
        users.GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await sut.GetCurrentAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task RegisterAsync_rejects_minor_adult_and_does_not_persist()
    {
        var result = await sut.RegisterAsync(NewRegister(birthDate: MinorBirthDate));

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.RegisterAdultCannotBeMinor);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegisterAsync_returns_not_found_when_adult_role_missing()
    {
        ExistsReturns(true);
        userTypes
            .FindAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((UserType?)null);

        var result = await sut.RegisterAsync(NewRegister());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task RegisterAsync_rejects_disallowed_adult_role(
        bool hidden,
        bool allowedForAdults
    )
    {
        ExistsReturns(true);
        userTypes
            .FindAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(NewUserType(hidden: hidden, allowedForAdults: allowedForAdults));

        var result = await sut.RegisterAsync(NewRegister());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotAllowedForAdults);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData("   ", "+34123456789", "password123")]
    [InlineData("ana@test.com", "   ", "password123")]
    [InlineData("ana@test.com", "+34123456789", "   ")]
    public async Task RegisterAsync_requires_contact_info(
        string email,
        string phone,
        string password
    )
    {
        ExistsReturns(false);

        var result = await sut.RegisterAsync(
            NewRegister(email: email, phone: phone, password: password)
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.RegisterContactInfoRequired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegisterAsync_returns_conflict_when_email_or_phone_in_use()
    {
        ExistsReturns(false, true);

        var result = await sut.RegisterAsync(NewRegister());

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.RegisterEmailOrPhoneAlreadyInUse);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegisterAsync_rejects_minor_with_adult_birthdate()
    {
        ExistsReturns(false, false);

        var result = await sut.RegisterAsync(
            NewRegister(minors: [NewMinor(birthDate: AdultBirthDate)])
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.RegisterMinorBirthDateNotMinor);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegisterAsync_returns_not_found_when_a_minor_role_is_missing()
    {
        ExistsReturns(false, false);
        userTypes
            .GetAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserType>());

        var result = await sut.RegisterAsync(NewRegister(minors: [NewMinor()]));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task RegisterAsync_rejects_disallowed_minor_role(
        bool hidden,
        bool allowedForMinors
    )
    {
        var minorRoleId = SeedIds.UserTypes.Participant;
        ExistsReturns(false, false);
        userTypes
            .GetAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
                new List<UserType>
                {
                    NewUserType(
                        id: minorRoleId,
                        hidden: hidden,
                        allowedForMinors: allowedForMinors
                    ),
                }
            );

        var result = await sut.RegisterAsync(NewRegister(minors: [NewMinor(roleId: minorRoleId)]));

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotAllowedForMinors);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegisterAsync_first_user_becomes_admin_with_member_type_and_persists()
    {
        clock.UtcNow = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var result = await sut.RegisterAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        result.Value.Minors.Should().BeEmpty();
        result.Value.RequiresVerification.Should().BeTrue();

        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u =>
                    u.FirstName == "Ana"
                    && u.LastName == "Ruiz"
                    && u.Email == "ana@test.com"
                    && u.Phone == "+34123456789"
                    && u.PasswordHash == "fake:password123"
                    && u.UserStatusTypeId == SeedIds.UserStatusTypes.Pending
                    && u.IsAdmin
                    && u.UserTypeId == SeedIds.UserTypes.Member
                    && u.OtpCodeHash != null
                    && u.OtpCodeHash.StartsWith(FakePasswordHasher.Prefix, StringComparison.Ordinal)
                    && u.OtpExpiresAt == clock.UtcNow + verification.OtpLifetime
                    && u.OtpLastSentAt == clock.UtcNow
                    && u.CreatedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await userTypes.DidNotReceiveWithAnyArgs().FindAsync(default!, default);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_sends_a_guid_otp_by_email_hashed_at_rest_and_not_in_the_response()
    {
        var added = CaptureAddedUsers();
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var result = await sut.RegisterAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().HaveCount(1);
        var code = emailSender.LastCode();
        Guid.TryParse(code, out _).Should().BeTrue("the OTP is now a GUID");

        var email = emailSender.Sent[0];
        email.ToAddress.Should().Be("ana@test.com");
        email.ToName.Should().Be("Ana");
        email.TextBody.Should().Contain(code);
        email.Subject.Should().NotContain(code);
        // The email carries a self-contained verification link (userId + code) so a user who loses
        // the registration tab can still verify.
        email.TextBody.Should().Contain("https://app.test/verify-account?userId=");
        email.HtmlBody.Should().Contain("/verify-account?userId=");

        // The OTP is stored hashed, never in plaintext.
        added.Should().ContainSingle();
        added[0].OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + code);
    }

    [Fact]
    public async Task RegisterAsync_still_succeeds_and_clears_last_sent_when_the_email_fails()
    {
        var added = CaptureAddedUsers();
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var result = await sut.RegisterAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresVerification.Should().BeTrue();
        // OtpLastSentAt is cleared so the failed delivery does not block the first resend.
        added.Should().ContainSingle();
        added[0].OtpLastSentAt.Should().BeNull();
        added[0]
            .OtpCodeHash.Should()
            .NotBeNull("the code is still issued so a resend can replace it");
        // One save to persist the account, one to clear OtpLastSentAt after the send failed.
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_creates_active_user_without_otp_when_verification_not_required()
    {
        verification.Required = false;
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var result = await sut.RegisterAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresVerification.Should().BeFalse();
        emailSender.Sent.Should().BeEmpty();
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u =>
                    u.UserStatusTypeId == SeedIds.UserStatusTypes.Active
                    && u.OtpCodeHash == null
                    && u.OtpExpiresAt == null
                    && u.OtpLastSentAt == null
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RegisterAsync_subsequent_user_creates_adult_with_requested_role_and_minor()
    {
        clock.UtcNow = new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero);
        var adultRoleId = SeedIds.UserTypes.Volunteer;
        var minorRoleId = SeedIds.UserTypes.Participant;
        ExistsReturns(true, false);
        userTypes
            .FindAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(NewUserType(id: adultRoleId));
        userTypes
            .GetAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new List<UserType> { NewUserType(id: minorRoleId) });
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<User> { NewUser(email: null) });

        var result = await sut.RegisterAsync(
            NewRegister(roleId: adultRoleId, minors: [NewMinor(roleId: minorRoleId)])
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Minors.Should().HaveCount(1);

        await users.Received(2).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u =>
                    u.UserStatusTypeId == SeedIds.UserStatusTypes.Pending
                    && !u.IsAdmin
                    && u.UserTypeId == adultRoleId
                ),
                Arg.Any<CancellationToken>()
            );
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u =>
                    u.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
                    && u.FirstName == "Leo"
                    && u.ParentId != null
                    && u.UserTypeId == minorRoleId
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_returns_not_found_when_user_missing()
    {
        FindReturns(null);

        var result = await sut.VerifyAsync(Guid.NewGuid(), "123456");

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task VerifyAsync_rejects_users_that_are_not_pending()
    {
        var user = FindReturns(
            NewUser(
                statusId: SeedIds.UserStatusTypes.Active,
                otpCodeHash: FakePasswordHasher.Prefix + "123456",
                otpExpiresAt: clock.UtcNow.AddMinutes(5)
            )
        );

        var result = await sut.VerifyAsync(user.Id, "123456");

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.OtpInvalidOrExpired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    public static TheoryData<string, bool, int?> InvalidOtpCases() =>
        new()
        {
            // otp argument, has stored hash, expiry offset in minutes (null = no expiry)
            { "   ", true, 5 },
            { "123456", false, 5 },
            { "123456", true, null },
            { "123456", true, -5 },
        };

    [Theory]
    [MemberData(nameof(InvalidOtpCases))]
    public async Task VerifyAsync_returns_bad_request_for_invalid_or_expired_otp(
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

        var result = await sut.VerifyAsync(user.Id, otpArgument);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.OtpInvalidOrExpired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task VerifyAsync_rejects_a_wrong_code_without_persisting()
    {
        var user = FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));

        var result = await sut.VerifyAsync(user.Id, "a-wrong-code");

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.OtpInvalidOrExpired);
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Pending);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task VerifyAsync_activates_user_clears_otp_and_persists()
    {
        var user = FindReturns(NewPendingWithOtp(clock, code: "the-real-code"));
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id));

        var result = await sut.VerifyAsync(user.Id, "  the-real-code  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationAsync_returns_not_found_when_user_missing()
    {
        FindReturns(null);

        var result = await sut.ResendVerificationAsync(Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_rejects_users_that_are_not_pending()
    {
        var user = FindReturns(NewUser(statusId: SeedIds.UserStatusTypes.Active));

        var result = await sut.ResendVerificationAsync(user.Id);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_rejects_a_pending_user_without_an_email()
    {
        var user = FindReturns(NewPendingWithOtp(clock));
        user.Email = null;

        var result = await sut.ResendVerificationAsync(user.Id);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_allows_an_immediate_resend_when_never_sent_before()
    {
        // A pending row with a null OtpLastSentAt (and null OtpCodeHash) can resend immediately;
        // the cooldown must not block it.
        var user = FindReturns(
            NewUser(
                statusId: SeedIds.UserStatusTypes.Pending,
                otpCodeHash: null,
                otpExpiresAt: null,
                otpLastSentAt: null
            )
        );

        var result = await sut.ResendVerificationAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        user.OtpCodeHash.Should().Be(FakePasswordHasher.Prefix + emailSender.LastCode());
        user.OtpLastSentAt.Should().Be(clock.UtcNow);
        emailSender.Sent.Should().HaveCount(1);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationAsync_rejects_when_verification_not_required()
    {
        verification.Required = false;
        var user = FindReturns(NewPendingWithOtp(clock));

        var result = await sut.ResendVerificationAsync(user.Id);

        result.Error!.Code.Should().Be(ErrorCode.OtpResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task ResendVerificationAsync_enforces_the_cooldown()
    {
        var user = FindReturns(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddSeconds(-10))
        );

        var result = await sut.ResendVerificationAsync(user.Id);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.OtpResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ResendVerificationAsync_issues_a_new_code_sends_it_and_persists()
    {
        var user = FindReturns(
            NewPendingWithOtp(clock, code: "old-code", otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );

        var result = await sut.ResendVerificationAsync(user.Id);

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
    public async Task ResendVerificationAsync_does_not_persist_a_new_code_when_the_email_fails()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = FindReturns(
            NewPendingWithOtp(clock, otpLastSentAt: clock.UtcNow.AddMinutes(-5))
        );
        var previousHash = user.OtpCodeHash;

        var act = () => sut.ResendVerificationAsync(user.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        user.OtpCodeHash.Should().Be(previousHash);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }
}
