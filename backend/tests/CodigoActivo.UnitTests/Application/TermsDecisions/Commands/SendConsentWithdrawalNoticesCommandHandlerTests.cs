using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Legal;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Application.TermsDecisions.Commands;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.TermsDecisions.Commands;

public sealed class SendConsentWithdrawalNoticesCommandHandlerTests
{
    private static readonly DateTimeOffset WithdrawnAt = new(2026, 7, 4, 10, 30, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly IEventTermsAcceptanceRepository acceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private readonly IPendingEmails pendingEmails = Substitute.For<IPendingEmails>();
    private readonly LegalEntityOptions legalEntity = new()
    {
        Name = "Asociación",
        Email = "legal@asociacion.test",
    };
    private readonly UserRow leo = new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Leo",
        LastName = "Ruiz",
    };
    private readonly UserRow marta = new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Marta",
        LastName = "Ruiz",
    };
    private readonly EventRow fair = new() { Id = Guid.NewGuid(), Title = "Feria de verano" };
    private readonly TermsDocumentRow image = new()
    {
        Id = Guid.NewGuid(),
        Name = "Autorización de imagen",
    };
    private readonly TermsDocumentRow name = new()
    {
        Id = Guid.NewGuid(),
        Name = "Publicación del nombre",
    };

    public SendConsentWithdrawalNoticesCommandHandlerTests()
    {
        store.Users.AddRange(leo, marta);
    }

    private EventTermsAcceptance Withdrawn(TermsDocumentRow document)
    {
        var acceptance = EventTermsAcceptance.Record(
            EventId.From(fair.Id),
            UserId.From(leo.Id),
            UserId.From(marta.Id),
            TermsDocumentId.From(document.Id),
            1,
            accepted: true,
            signature: null,
            UserId.From(marta.Id),
            WithdrawnAt.AddDays(-30)
        );
        acceptance.Withdraw(UserId.From(marta.Id), UserId.From(marta.Id), WithdrawnAt);
        store.EventTermsAcceptances.Add(
            new EventTermsAcceptanceRow
            {
                EventId = fair.Id,
                Event = fair,
                UserId = leo.Id,
                User = leo,
                DecidedById = marta.Id,
                DecidedBy = marta,
                TermsDocumentId = document.Id,
                TermsDocument = document,
                Accepted = false,
                DecidedAt = WithdrawnAt,
                Changes = [new TermsDecisionChangeRow { Id = 7, RecordedById = marta.Id }],
            }
        );
        return acceptance;
    }

    private void Pending(params EventTermsAcceptance[] pending)
    {
        acceptances
            .ListWithdrawalNoticesPendingAsync(
                ConsentWithdrawalNotifier.MaxPerNotice,
                Arg.Any<CancellationToken>()
            )
            .Returns(pending);
    }

    private async Task<int> HandleAsync()
    {
        var handler = new SendConsentWithdrawalNoticesCommandHandler(
            acceptances,
            legalEntity,
            pendingEmails,
            new ConsentWithdrawalNotifier(
                store,
                new FakeQueryExecutor(),
                legalEntity,
                emailSender,
                new ConsentWithdrawalEmailComposer(
                    new ApplicationOptions { BaseUrl = "https://app.test/" },
                    new TestClock()
                )
            )
        );
        var result = await handler.HandleAsync(
            new SendConsentWithdrawalNoticesCommand(),
            TestContext.Current.CancellationToken
        );
        return result.Value;
    }

    [Fact]
    public async Task HandleAsyncOneWithdrawalNamesItWhoRecordedItAndRecordsTheNotice()
    {
        var withdrawal = Withdrawn(image);
        Pending(withdrawal);

        var told = await HandleAsync();

        told.Should().Be(1);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("legal@asociacion.test");
        message.ToName.Should().Be("Asociación");
        message.Topic.Should().BeNull("a new summary never replaces the one still waiting");
        message.Subject.Should().Be("Se ha retirado un consentimiento");
        message
            .TextBody.Should()
            .Contain("«Autorización de imagen» de Leo Ruiz para el evento «Feria de verano»")
            .And.Contain("Lo registró Marta Ruiz el 04/07/2026, 10:30 h.");
        withdrawal.WithdrawalNoticePending.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncSeveralWithdrawalsGoInOneEmail()
    {
        var first = Withdrawn(image);
        var second = Withdrawn(name);
        Pending(first, second);

        var told = await HandleAsync();

        told.Should().Be(2);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Subject.Should().Be("Se han retirado 2 consentimientos");
        message
            .TextBody.Should()
            .Contain("«Autorización de imagen»")
            .And.Contain("«Publicación del nombre»");
        first.WithdrawalNoticePending.Should().BeFalse();
        second.WithdrawalNoticePending.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncPreviousNoticeStillWaitingKeepsTheWithdrawalsForTheNextOne()
    {
        var withdrawal = Withdrawn(image);
        Pending(withdrawal);
        pendingEmails
            .AnyAsync(EmailKind.ConsentWithdrawalNotice, Arg.Any<CancellationToken>())
            .Returns(true);

        var told = await HandleAsync();

        told.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        withdrawal.WithdrawalNoticePending.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncWithoutALegalContactSendsNothingAndKeepsThemWaiting()
    {
        var withdrawal = Withdrawn(image);
        Pending(withdrawal);
        legalEntity.Email = " ";

        var told = await HandleAsync();

        told.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        withdrawal.WithdrawalNoticePending.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncNothingWaitingSendsNothing()
    {
        Pending();

        var told = await HandleAsync();

        told.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncNoticeNotQueuedKeepsTheWithdrawalsWaiting()
    {
        var withdrawal = Withdrawn(image);
        Pending(withdrawal);
        emailSender.ThrowOnSend = new InvalidOperationException("database down");

        var act = HandleAsync;

        await act.Should().ThrowAsync<InvalidOperationException>();
        withdrawal.WithdrawalNoticePending.Should().BeTrue();
    }
}
