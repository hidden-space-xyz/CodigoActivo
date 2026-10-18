using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails.Commands;

public sealed class UnsubscribeCommandHandlerTests
{
    private readonly DataProtectionUnsubscribeTokens tokens = new(
        new EphemeralDataProtectionProvider()
    );

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly AuditSubject auditSubject = new();
    private readonly UnsubscribeCommandHandler sut;

    public UnsubscribeCommandHandlerTests()
    {
        sut = new UnsubscribeCommandHandler(tokens, users, clock, auditSubject);
    }

    private static User Account()
    {
        return User.CreateIndependent(
            new PersonDetails(
                "Ana",
                "Ruiz",
                Gender.Female,
                "ana@test.com",
                "600111222",
                null,
                "12345678Z",
                PromotionalConsent: true
            ),
            new DateTimeOffset(2025, 1, 1, 9, 0, 0, TimeSpan.Zero)
        ).Value;
    }

    [Fact]
    public async Task HandleAsyncLinkOfAnAccountWithdrawsItsConsentThroughTheLink()
    {
        var account = Account();
        users.Finds(account);

        var result = await sut.HandleAsync(
            new UnsubscribeCommand(tokens.Issue(account.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        account.PromotionalConsent.Should().BeFalse();
        var withdrawal = account.PromotionalConsentChanges.Last();
        withdrawal.Source.Should().Be(PromotionalConsentSource.UnsubscribeLink);
        withdrawal.ChangedAt.Should().Be(clock.UtcNow);
        auditSubject.Entity.Should().Be(AuditEntity.Of(account.Id));
        await users.Received(1).GetByIdAsync(account.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncLinkOfAnErasedAccountSucceedsWithoutChangingAnything()
    {
        users.Finds(null);

        var result = await sut.HandleAsync(
            new UnsubscribeCommand(tokens.Issue(UserId.New())),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncAlteredLinkReturnsUnsubscribeLinkInvalid()
    {
        var token = tokens.Issue(UserId.New());

        var result = await sut.HandleAsync(
            new UnsubscribeCommand(token[..^4]),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailUnsubscribeLinkInvalid);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
    }
}
