using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Users;
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
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly FakePasswordHasher hasher = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly AccountVerificationOptions verification = new();
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
            hasher,
            verification,
            new AccountEmails(
                emailSender,
                new AccountEmailComposer(application, new TestClock()),
                verification,
                new PasswordResetOptions(),
                new TwoFactorOptions()
            ),
            NullLogger<RegisterCommandHandler>.Instance,
            cacheInvalidator,
            new DisposableEmailChecker(disposableDomains),
            new EmailClaims(AccountErasers.Create(users, deletedAccounts, erasureStore, uow), uow)
        );
    }

    private Task<Result> HandleAsync(RegisterRequest request)
    {
        return sut.HandleAsync(new RegisterCommand(request), TestContext.Current.CancellationToken);
    }

    private async Task<List<User>> CaptureAddedUsersAsync()
    {
        var added = new List<User>();
        await users.AddAsync(Arg.Do<User>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    private User HolderOfTheAddress(Guid statusId)
    {
        var holder = NewUser(email: "ana@test.com", statusId: statusId);
        users.GetByEmailAsync("ana@test.com", Arg.Any<CancellationToken>()).Returns(holder);
        return holder;
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

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private ValueTask AssertNotInvalidatedAsync()
    {
        return cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
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

        var result = await HandleAsync(
            NewRegister(nationalId: " x-1234567-l ", promotionalConsent: true, minors: [NewMinor()])
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().HaveCount(2);
        var adult = added[0];
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
        var result = await HandleAsync(NewRegister(secondaryPhone: " +34123456789 "));

        result.ShouldFail(ErrorKind.Validation, ErrorCode.SecondaryPhoneSameAsPrimary);
        await AssertNotSavedAsync();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByEmailAsync(default!, TestContext.Current.CancellationToken);
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncBlankPasswordReturnsBadRequest()
    {
        var result = await HandleAsync(NewRegister(password: "   "));

        result.ShouldFail(ErrorKind.Validation, ErrorCode.RequestValidationFailed);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAddressOwnedByAnAccountGetsANoticeAndNothingIsCreated()
    {
        var holder = HolderOfTheAddress(SeedIds.UserStatusTypes.Active);

        var result = await HandleAsync(NewRegister(minors: [NewMinor()]));

        result.IsSuccess.Should().BeTrue();
        hasher.Hashes.Should().Be(2, "every outcome hashes the password and the code");
        var notice = emailSender.Sent.Should().ContainSingle().Subject;
        notice.Kind.Should().Be(EmailKind.AccountVerification);
        notice.ToAddress.Should().Be(holder.Email);
        notice.Subject.Should().Be(AppStrings.EmailsEmailInUseSubject);
        notice.TextBody.Should().NotContain("verify-account");
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
        await AssertNotInvalidatedAsync();
    }

    [Fact]
    public async Task HandleAsyncNoticeRefusedByTheLimiterStillSucceeds()
    {
        HolderOfTheAddress(SeedIds.UserStatusTypes.Active);
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        var result = await HandleAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncNoticeThatCannotBeQueuedStillSucceeds()
    {
        HolderOfTheAddress(SeedIds.UserStatusTypes.Blocked);
        emailSender.ThrowOnSend = new InvalidOperationException("outbox unavailable");

        var result = await HandleAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAddressOfAnUnverifiedAccountReplacesItWithTheNewOne()
    {
        var holder = HolderOfTheAddress(SeedIds.UserStatusTypes.Pending);
        var added = await CaptureAddedUsersAsync();

        var result = await HandleAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        var adult = added.Should().ContainSingle().Subject;
        adult.Id.Should().NotBe(holder.Id);
        users.Received(1).Remove(holder);
        await erasureStore
            .Received(1)
            .CaptureLegalCopyAsync(
                holder.Id,
                Arg.Is<AccountErasure>(erasure =>
                    erasure.Origin == AccountDeletionOrigin.EmailClaimed
                    && erasure.ActorId == adult.Id
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        emailSender
            .Sent.Should()
            .ContainSingle()
            .Which.TextBody.Should()
            .Contain($"verify-account#userId={adult.Id}&code=");
    }

    [Fact]
    public async Task HandleAsyncAddressTakenByAConcurrentRegistrationSendsNothing()
    {
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(User),
                    new InvalidOperationException("duplicate")
                )
            );

        var result = await HandleAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotInvalidatedAsync();
    }

    [Theory]
    [InlineData("ana@mailinator.com")]
    [InlineData("  Ana@Inbox.MAILINATOR.com  ")]
    public async Task HandleAsyncDisposableEmailReturnsBadRequestWithoutLookingUpAccounts(
        string email
    )
    {
        disposableDomains.Add("mailinator.com");

        var result = await HandleAsync(NewRegister(email: email));

        result.ShouldFail(ErrorKind.Validation, ErrorCode.DisposableEmailNotAllowed);
        await AssertNotSavedAsync();
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByEmailAsync(default!, TestContext.Current.CancellationToken);
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncEmailOutsideTheDisposableListIsRegistered()
    {
        disposableDomains.Add("mailinator.com");

        var result = await HandleAsync(NewRegister(email: "ana@notmailinator.com"));

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
        var result = await HandleAsync(NewRegister(minors: [NewMinor(birthDate: AdultBirthDate)]));

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserChildBirthDateNotMinor);
        await AssertNotSavedAsync();
        await users
            .DidNotReceiveWithAnyArgs()
            .AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTooManyMinorsReturnsBadRequest()
    {
        var minors = Enumerable.Range(0, 21).Select(_ => NewMinor()).ToList();

        var result = await HandleAsync(NewRegister(minors: minors));

        result.ShouldFail(ErrorKind.Validation, ErrorCode.RequestValidationFailed);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncFirstOrdinaryUserIsNotPromotedToAdmin()
    {
        clock.UtcNow = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        var added = await CaptureAddedUsersAsync();

        var result = await HandleAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle();
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

        var result = await HandleAsync(NewRegister());

        result.IsSuccess.Should().BeTrue();
        hasher.Hashes.Should().Be(2);
        emailSender.Sent.Should().HaveCount(1);
        var code = emailSender.LastCode();
        code.Should().MatchRegex("^[0-9a-f]{64}$", "the OTP is 256 random bits in lowercase hex");

        var email = emailSender.Sent[0];
        email.ToAddress.Should().Be("ana@test.com");
        email
            .ToName.Should()
            .BeEmpty("an unverified address may belong to someone else than the registrant");
        email.TextBody.Should().NotContain("Ana");
        email.HtmlBody.Should().NotContain("Ana");
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

        var result = await HandleAsync(NewRegister());

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

        var act = () => HandleAsync(NewRegister());

        await act.Should().ThrowAsync<OperationCanceledException>();
        added.Should().ContainSingle();
        added[0].OtpLastSentAt.Should().Be(clock.UtcNow, "the swallow-and-clear catch is skipped");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncSubsequentUserWithMinorCreatesAdultAndMinorAsParticipants()
    {
        clock.UtcNow = new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero);
        var added = await CaptureAddedUsersAsync();

        var result = await HandleAsync(NewRegister(minors: [NewMinor()]));

        result.IsSuccess.Should().BeTrue();
        added.Should().HaveCount(2);
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
