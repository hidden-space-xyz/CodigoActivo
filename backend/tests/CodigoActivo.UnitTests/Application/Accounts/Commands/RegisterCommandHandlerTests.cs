using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

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
    private readonly FakeDisposableEmailDomainRepository disposableDomains = new();
    private readonly RegisterCommandHandler sut;

    public RegisterCommandHandlerTests()
    {
        sut = new RegisterCommandHandler(
            users,
            uow,
            clock,
            new FakePasswordHasher(),
            verification,
            new AccountEmails(
                emailSender,
                new AccountEmailComposer(application, new TestClock()),
                verification,
                passwordReset,
                new TwoFactorOptions()
            ),
            NullLogger<RegisterCommandHandler>.Instance,
            cacheInvalidator,
            new DisposableEmailChecker(disposableDomains)
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
        string nationalId = "12345678Z",
        Gender gender = Gender.Female,
        bool promotionalConsent = false,
        IReadOnlyList<RegisterMinorRequest>? minors = null,
        string? secondaryPhone = null
    )
    {
        return new(
            "  Ana  ",
            "  Ruiz  ",
            email,
            phone,
            password,
            nationalId,
            gender,
            promotionalConsent,
            minors,
            secondaryPhone
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
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(seq[0], [.. seq.Skip(1)]);
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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
    public async Task HandleAsyncNewAdultStoresNationalIdAndConsentButNoBirthDate()
    {
        var added = await CaptureAddedUsersAsync();
        ExistsReturns(false);

        var result = await sut.HandleAsync(
            new RegisterCommand(
                NewRegister(
                    nationalId: " x-1234567-l ",
                    promotionalConsent: true,
                    minors: [NewMinor()]
                )
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().HaveCount(2);
        var adult = added[0];
        result.Value.Should().Be(adult.Id);
        adult.NationalId.Should().Be("X1234567L");
        adult.PromotionalConsent.Should().BeTrue();
        adult.BirthDate.Should().BeNull();
        var minor = added[1];
        minor.NationalId.Should().BeNull();
        minor.PromotionalConsent.Should().BeFalse();
        minor.BirthDate.Should().Be(MinorBirthDate);
    }

    [Fact]
    public async Task HandleAsyncBrokenProfileRuleIsRefusedWithoutAddingAnyone()
    {
        ExistsReturns(false);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(secondaryPhone: " +34123456789 ")),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.SecondaryPhoneSameAsPrimary);
        await AssertNotSavedAsync();
        await users
            .DidNotReceiveWithAnyArgs()
            .EmailExistsAsync(default!, default, TestContext.Current.CancellationToken);
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncBlankPasswordReturnsBadRequest()
    {
        ExistsReturns(false);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(password: "   ")),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.RequestValidationFailed);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncEmailInUseReturnsConflict()
    {
        ExistsReturns(true);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserEmailAlreadyInUse);
        await AssertNotSavedAsync();
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Theory]
    [InlineData("ana@mailinator.com")]
    [InlineData("  Ana@Inbox.MAILINATOR.com  ")]
    public async Task HandleAsyncDisposableEmailReturnsBadRequestWithoutLookingUpAccounts(
        string email
    )
    {
        disposableDomains.Add("mailinator.com");

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(email: email)),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.DisposableEmailNotAllowed);
        await AssertNotSavedAsync();
        await users
            .DidNotReceiveWithAnyArgs()
            .EmailExistsAsync(default!, default, TestContext.Current.CancellationToken);
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncEmailOutsideTheDisposableListIsRegistered()
    {
        disposableDomains.Add("mailinator.com");
        ExistsReturns(false);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(email: "ana@notmailinator.com")),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        disposableDomains
            .Lookups.Should()
            .ContainSingle()
            .Which.Should()
            .Equal("notmailinator.com");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncMinorWithAdultBirthDateReturnsBadRequest()
    {
        ExistsReturns(false, false);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(minors: [NewMinor(birthDate: AdultBirthDate)])),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserChildBirthDateNotMinor);
        await AssertNotSavedAsync();
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTooManyMinorsReturnsBadRequest()
    {
        ExistsReturns(false, false);
        var minors = Enumerable.Range(0, 21).Select(_ => NewMinor()).ToList();

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(minors: minors)),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ErrorCode.RequestValidationFailed);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncFirstOrdinaryUserIsNotPromotedToAdmin()
    {
        clock.UtcNow = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        ExistsReturns(false);
        var added = await CaptureAddedUsersAsync();

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var adult = added.Should().ContainSingle().Which;
        result.Value.Should().Be(adult.Id);

        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => IsPendingParticipantAdult(u)),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncNewAdultSendsGuidOtpHashedAtRestAndInvalidatesCache()
    {
        var added = await CaptureAddedUsersAsync();
        ExistsReturns(false, false);

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().HaveCount(1);
        var code = emailSender.LastCode();
        code.Should().MatchRegex("^[0-9a-f]{64}$", "the OTP is 256 random bits in lowercase hex");

        var email = emailSender.Sent[0];
        email.ToAddress.Should().Be("ana@test.com");
        email.ToName.Should().Be("Ana");
        email.TextBody.Should().Contain(code);
        email.Subject.Should().NotContain(code);
        email.TextBody.Should().Contain("https://app.test/verify-account#userId=");
        email.HtmlBody.Should().Contain("/verify-account#userId=");

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

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
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
        ExistsReturns(false);
        var added = await CaptureAddedUsersAsync();

        var result = await sut.HandleAsync(
            new RegisterCommand(NewRegister(minors: [NewMinor()])),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().HaveCount(2);
        result.Value.Should().Be(added[0].Id);
        added[1].ParentId.Should().Be(added[0].Id);

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
