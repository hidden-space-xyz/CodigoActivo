using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

/// <summary>
/// Unit tests for <see cref="AuthService"/>. Collaborators are NSubstitute doubles; the real
/// <see cref="FakePasswordHasher"/> and <see cref="TestClock"/> preserve the hash/verify and time
/// contracts. Every guard branch and every distinct <see cref="ErrorCode"/> is exercised, asserting
/// both <see cref="Error.Kind"/> and <see cref="Error.Code"/> and that failures never persist.
/// </summary>
public sealed class AuthServiceTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserTypeRepository userTypes = Substitute.For<IUserTypeRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly AuthService sut;

    public AuthServiceTests()
    {
        sut = new AuthService(users, userTypes, uow, clock, new FakePasswordHasher());
    }

    private static readonly DateOnly AdultBirthDate = new(1990, 1, 1);
    private static readonly DateOnly MinorBirthDate = new(2020, 1, 1);

    private static User NewUser(
        Guid? id = null,
        string? email = "ana@test.com",
        string? passwordHash = "fake:password123",
        Guid? statusId = null,
        Guid? otpCode = null,
        DateTimeOffset? otpExpiresAt = null
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
            OtpCode = otpCode,
            OtpExpiresAt = otpExpiresAt,
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
        new("  Leo  ", "  Ruiz  ", birthDate ?? MinorBirthDate, roleId ?? SeedIds.UserTypes.Participant);

    /// <summary>Stubs the sequential <c>users.ExistsAsync</c> calls: first is the "is first user" probe,
    /// the second (if reached) is the duplicate email/phone probe.</summary>
    private void ExistsReturns(params bool[] seq) =>
        users
            .ExistsAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(seq[0], seq.Skip(1).ToArray());

    // ---- LoginAsync --------------------------------------------------------

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
    public async Task LoginAsync_succeeds_trims_identifier_records_login_and_maps_response()
    {
        var user = NewUser();
        users
            .GetByEmailOrPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await sut.LoginAsync(new LoginRequest("  ana@test.com  ", "password123"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("ana@test.com");
        user.LastLoginAt.Should().NotBeNull();
        await users.Received(1).GetByEmailOrPhoneAsync("ana@test.com", Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- GetCurrentAsync ---------------------------------------------------

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
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await sut.GetCurrentAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
    }

    // ---- RegisterAsync : guards -------------------------------------------

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
    [InlineData(true, true)] // hidden
    [InlineData(false, false)] // not allowed for adults
    public async Task RegisterAsync_rejects_disallowed_adult_role(bool hidden, bool allowedForAdults)
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
    [InlineData("   ", "+34123456789", "password123")] // blank email
    [InlineData("ana@test.com", "   ", "password123")] // blank phone
    [InlineData("ana@test.com", "+34123456789", "   ")] // blank password
    public async Task RegisterAsync_requires_contact_info(string email, string phone, string password)
    {
        ExistsReturns(false); // first user -> role check skipped

        var result = await sut.RegisterAsync(NewRegister(email: email, phone: phone, password: password));

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.RegisterContactInfoRequired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegisterAsync_returns_conflict_when_email_or_phone_in_use()
    {
        ExistsReturns(false, true); // first user, but duplicate contact

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
            .Returns(new List<UserType>()); // fewer than the distinct requested role ids

        var result = await sut.RegisterAsync(NewRegister(minors: [NewMinor()]));

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(true, true)] // hidden
    [InlineData(false, false)] // not allowed for minors
    public async Task RegisterAsync_rejects_disallowed_minor_role(bool hidden, bool allowedForMinors)
    {
        var minorRoleId = SeedIds.UserTypes.Participant;
        ExistsReturns(false, false);
        userTypes
            .GetAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(
                new List<UserType>
                {
                    NewUserType(id: minorRoleId, hidden: hidden, allowedForMinors: allowedForMinors),
                }
            );

        var result = await sut.RegisterAsync(NewRegister(minors: [NewMinor(roleId: minorRoleId)]));

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotAllowedForMinors);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    // ---- RegisterAsync : success ------------------------------------------

    [Fact]
    public async Task RegisterAsync_first_user_gets_admin_and_member_roles_and_persists()
    {
        clock.UtcNow = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        ExistsReturns(false, false); // first user, no duplicate
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<User>());

        var result = await sut.RegisterAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        result.Value.Minors.Should().BeEmpty();
        result.Value.VerificationCode.Should().HaveValue();
        result.Value.VerificationCode!.Value.Should().NotBe(Guid.Empty);

        await users.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.FirstName == "Ana"
                && u.LastName == "Ruiz"
                && u.Email == "ana@test.com"
                && u.Phone == "+34123456789"
                && u.PasswordHash == "fake:password123"
                && u.UserStatusTypeId == SeedIds.UserStatusTypes.Pending
                && u.OtpCode != null
                && u.OtpCode != Guid.Empty
                && u.OtpExpiresAt == clock.UtcNow.AddMinutes(15)
                && u.CreatedAt == clock.UtcNow
            ),
            Arg.Any<CancellationToken>()
        );
        await users.Received(1).AddTypeAssignmentAsync(
            Arg.Is<UserTypeAssignment>(a => a.UserTypeId == SeedIds.UserTypes.Admin),
            Arg.Any<CancellationToken>()
        );
        await users.Received(1).AddTypeAssignmentAsync(
            Arg.Is<UserTypeAssignment>(a => a.UserTypeId == SeedIds.UserTypes.Member),
            Arg.Any<CancellationToken>()
        );
        // Only the adult is created; the role check was skipped for the first user.
        await userTypes.DidNotReceiveWithAnyArgs().FindAsync(default!, default);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_subsequent_user_creates_adult_with_requested_role_and_minor()
    {
        clock.UtcNow = new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero);
        var adultRoleId = SeedIds.UserTypes.Volunteer;
        var minorRoleId = SeedIds.UserTypes.Participant;
        ExistsReturns(true, false); // not first user, no duplicate
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

        // Adult (Pending) and one child (Dependent) are added.
        await users.Received(2).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await users.Received(1).AddAsync(
            Arg.Is<User>(u => u.UserStatusTypeId == SeedIds.UserStatusTypes.Pending),
            Arg.Any<CancellationToken>()
        );
        await users.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
                && u.FirstName == "Leo"
                && u.ParentId != null
            ),
            Arg.Any<CancellationToken>()
        );
        await users.Received(1).AddTypeAssignmentAsync(
            Arg.Is<UserTypeAssignment>(a => a.UserTypeId == adultRoleId),
            Arg.Any<CancellationToken>()
        );
        await users.Received(1).AddTypeAssignmentAsync(
            Arg.Is<UserTypeAssignment>(a => a.UserTypeId == minorRoleId),
            Arg.Any<CancellationToken>()
        );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- VerifyAsync -------------------------------------------------------

    [Fact]
    public async Task VerifyAsync_returns_not_found_when_user_missing()
    {
        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await sut.VerifyAsync(Guid.NewGuid(), Guid.NewGuid().ToString());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    public static TheoryData<string, Guid?, int?> InvalidOtpCases()
    {
        var code = Guid.NewGuid();
        return new TheoryData<string, Guid?, int?>
        {
            { "   ", code, 5 }, // blank otp argument
            { Guid.NewGuid().ToString(), Guid.Empty, 5 }, // stored code is empty
            { Guid.NewGuid().ToString(), null, 5 }, // stored code is null
            { Guid.NewGuid().ToString(), code, null }, // no expiry set
            { Guid.NewGuid().ToString(), code, -5 }, // expired
            { Guid.NewGuid().ToString(), code, 5 }, // mismatch (argument != stored code)
        };
    }

    [Theory]
    [MemberData(nameof(InvalidOtpCases))]
    public async Task VerifyAsync_returns_bad_request_for_invalid_or_expired_otp(
        string otpArgument,
        Guid? storedCode,
        int? expiresInMinutes
    )
    {
        var user = NewUser(
            statusId: SeedIds.UserStatusTypes.Pending,
            otpCode: storedCode,
            otpExpiresAt: expiresInMinutes is null
                ? null
                : clock.UtcNow.AddMinutes(expiresInMinutes.Value)
        );
        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await sut.VerifyAsync(user.Id, otpArgument);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.OtpInvalidOrExpired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task VerifyAsync_activates_user_clears_otp_and_persists()
    {
        var code = Guid.NewGuid();
        var user = NewUser(
            statusId: SeedIds.UserStatusTypes.Pending,
            otpCode: code,
            otpExpiresAt: clock.UtcNow.AddMinutes(5)
        );
        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(user);
        users
            .GetByIdWithDetailsAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(NewUser(id: user.Id));

        var result = await sut.VerifyAsync(user.Id, code.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        user.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Active);
        user.OtpCode.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
