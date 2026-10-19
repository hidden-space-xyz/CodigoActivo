using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class SendComingOfAgeNoticesCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly User guardian = NewUser(first: "Marta", email: "marta@test.com");
    private readonly SendComingOfAgeNoticesCommandHandler sut;

    public SendComingOfAgeNoticesCommandHandlerTests()
    {
        sut = new SendComingOfAgeNoticesCommandHandler(
            users,
            new AccountEmails(
                emailSender,
                composer,
                new AccountVerificationOptions(),
                new PasswordResetOptions(),
                new TwoFactorOptions()
            ),
            uow.RunsTransactions(),
            clock,
            NullLogger<SendComingOfAgeNoticesCommandHandler>.Instance
        );
        users.LocksEveryAccount();
        users.GetByIdAsync(guardian.Id, Arg.Any<CancellationToken>()).Returns(guardian);
    }

    private User Dependent(DateOnly birthDate)
    {
        return NewUser(
            first: "Leo",
            last: "Ruiz",
            parentId: guardian.Id.Value,
            dob: birthDate,
            email: null,
            phone: null,
            statusId: KnownIds.UserStatusTypes.Dependent
        );
    }

    private void Lists(params User[] dependents)
    {
        users
            .ListDependentsBornUpToAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(dependents);
    }

    private Task<CodigoActivo.Domain.Common.Result<int>> HandleAsync()
    {
        return sut.HandleAsync(
            new SendComingOfAgeNoticesCommand(),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncTellsTheGuardianOfADependentWhoComesOfAgeWithinThirtyDays()
    {
        var comingOfAgeOn = clock.Today.AddDays(30);
        var dependent = Dependent(comingOfAgeOn.AddYears(-18));
        Lists(dependent);

        var result = await HandleAsync();

        result.Value.Should().Be(1);
        await users
            .Received(1)
            .ListDependentsBornUpToAsync(
                clock.Today.AddDays(30).AddYears(-18),
                Arg.Any<CancellationToken>()
            );
        var notice = composer.Composed.Should().ContainSingle().Subject;
        notice.Name.Should().Be(nameof(IAccountEmailComposer.ComingOfAgeNotice));
        notice.Recipient.Should().Be(new EmailRecipient("marta@test.com", "Marta"));
        notice.DependentName.Should().Be("Leo Ruiz");
        notice.ComingOfAgeOn.Should().Be(comingOfAgeOn);
        emailSender
            .Sent.Should()
            .ContainSingle()
            .Which.Kind.Should()
            .Be(EmailKind.ComingOfAgeNotice);
        dependent.ComingOfAgeNoticeSentAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncSendsNothingForADependentAlreadyNotified()
    {
        var dependent = Dependent(clock.Today.AddDays(10).AddYears(-18));
        dependent.RecordComingOfAgeNotice(clock.UtcNow.AddDays(-20));
        Lists(dependent);

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        await users
            .DidNotReceiveWithAnyArgs()
            .LockAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncLeavesTheNoticeDueWhenTheEmailLimitsRefuseIt()
    {
        var dependent = Dependent(clock.Today.AddDays(10).AddYears(-18));
        Lists(dependent);
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        dependent.ComingOfAgeNoticeSentAt.Should().BeNull();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncLeavesTheNoticeDueWhenTheEmailFails()
    {
        var dependent = Dependent(clock.Today.AddDays(10).AddYears(-18));
        Lists(dependent);
        emailSender.ThrowOnSend = new InvalidOperationException("Outbox unavailable.");

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        dependent.ComingOfAgeNoticeSentAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncSendsNothingToAGuardianWhoseEmailNobodyVerified()
    {
        Persisted.Overwrite(guardian, new { Status = UserStatus.Pending });
        var dependent = Dependent(clock.Today.AddDays(10).AddYears(-18));
        Lists(dependent);

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        dependent.ComingOfAgeNoticeSentAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncSkipsADependentWhoseBirthDateWasCorrectedSinceItWasListed()
    {
        var dependent = Dependent(clock.Today.AddDays(10).AddYears(-18));
        Lists(dependent);
        users.ReloadsAs(dependent, new { BirthDate = clock.Today.AddYears(-16) });

        var result = await HandleAsync();

        result.Value.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
    }
}
