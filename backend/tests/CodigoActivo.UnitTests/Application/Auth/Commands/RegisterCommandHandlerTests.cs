using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Auth.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Auth.Commands;

public sealed class RegisterCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly PasswordResetOptions passwordReset = new();
    private readonly ApplicationOptions application = new() { BaseUrl = "https://app.test" };
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly RegisterCommandHandler sut;

    public RegisterCommandHandlerTests()
    {
        sut = new RegisterCommandHandler(
            users,
            uow,
            clock,
            new FakePasswordHasher(),
            verification,
            new AccountEmails(emailSender, verification, passwordReset, application),
            NullLogger<RegisterCommandHandler>.Instance,
            cacheInvalidator
        );
    }

    private async Task<List<User>> CaptureAddedUsersAsync()
    {
        var added = new List<User>();
        await users.AddAsync(Arg.Do<User>(added.Add), Arg.Any<CancellationToken>());
        return added;
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

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static bool IsRegisteredFirstAdmin(User? user, DateTimeOffset now, TimeSpan otpLifetime)
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
            user.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
            && string.Equals(user.FirstName, "Leo", StringComparison.Ordinal);

        return isDependentLeo
            && user.Gender is Gender.Other
            && user.ParentId is not null
            && user.UserTypeId == SeedIds.UserTypes.Participant;
    }

    [Fact]
    public async Task HandleAsyncAdultBirthDateIsMinorReturnsBadRequest()
    {
        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(birthDate: MinorBirthDate)),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.RegisterAdultCannotBeMinor);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData("   ", "+34123456789", "password123")]
    [InlineData("ana@test.com", "   ", "password123")]
    [InlineData("ana@test.com", "+34123456789", "   ")]
    public async Task HandleAsyncMissingContactInfoReturnsBadRequest(
        string email,
        string phone,
        string password
    )
    {
        ExistsReturns(false);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(email: email, phone: phone, password: password)),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.RegisterContactInfoRequired);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailOrPhoneInUseReturnsConflict()
    {
        ExistsReturns(false, true);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.RegisterEmailOrPhoneAlreadyInUse);
        await AssertNotSavedAsync();
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsyncMinorWithAdultBirthDateReturnsBadRequest()
    {
        ExistsReturns(false, false);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(minors: [NewMinor(birthDate: AdultBirthDate)])),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.RegisterMinorBirthDateNotMinor);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncFirstUserBecomesAdminWithParticipantType()
    {
        clock.UtcNow = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

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
    public async Task HandleAsyncNewAdultSendsGuidOtpHashedAtRestAndInvalidatesCache()
    {
        var added = await CaptureAddedUsersAsync();
        ExistsReturns(false, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

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
    public async Task HandleAsyncEmailSendFailsSucceedsAndClearsLastSent()
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

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

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
    public async Task HandleAsyncEmailSendCancelledPropagatesOperationCanceledException()
    {
        var added = await CaptureAddedUsersAsync();
        emailSender.ThrowOnSend = new OperationCanceledException("registration cancelled");
        ExistsReturns(false, false);

        var act = () =>
            sut.HandleAsync(
                new RegisterCommand(NewRegister()),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<OperationCanceledException>();
        added.Should().ContainSingle();
        added[0].OtpLastSentAt.Should().Be(clock.UtcNow, "the swallow-and-clear catch is skipped");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncSubsequentUserWithMinorCreatesAdultAndMinorAsParticipants()
    {
        clock.UtcNow = new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero);
        ExistsReturns(true, false);
        users
            .GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NewUser());
        users
            .ListChildrenWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([NewUser(email: null)]);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(minors: [NewMinor()])),
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
}
