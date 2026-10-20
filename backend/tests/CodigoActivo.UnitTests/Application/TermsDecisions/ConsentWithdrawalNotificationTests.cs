using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Legal;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.TermsDecisions;

public sealed class ConsentWithdrawalNotificationTests
{
    private static readonly DateTimeOffset WithdrawnAt = new(2026, 7, 4, 10, 30, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly RecordingLogger<ConsentWithdrawalNotification> logger = new();
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
    private readonly EventRow ev = new() { Id = Guid.NewGuid(), Title = "Feria de verano" };
    private readonly TermsDocumentRow image = new()
    {
        Id = Guid.NewGuid(),
        Name = "Autorización de imagen",
    };
    private readonly ConsentWithdrawalNotification sut;

    public ConsentWithdrawalNotificationTests()
    {
        store.Users.AddRange(leo, marta);
        store.EventTermsAcceptances.Add(
            new EventTermsAcceptanceRow
            {
                EventId = ev.Id,
                Event = ev,
                UserId = leo.Id,
                User = leo,
                DecidedById = marta.Id,
                DecidedBy = marta,
                TermsDocumentId = image.Id,
                TermsDocument = image,
                Accepted = false,
                DecidedAt = WithdrawnAt,
            }
        );
        sut = new ConsentWithdrawalNotification(
            store,
            new FakeQueryExecutor(),
            legalEntity,
            emailSender,
            new ConsentWithdrawalEmailComposer(
                new ApplicationOptions { BaseUrl = "https://app.test/" },
                new TestClock()
            ),
            logger
        );
    }

    private Task NotifyAsync()
    {
        return sut.HandleAsync(
            new TermsConsentWithdrawn(
                EventId.From(ev.Id),
                UserId.From(leo.Id),
                TermsDocumentId.From(image.Id),
                UserId.From(marta.Id)
            ),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncTellsTheLegalContactWhatWasWithdrawnAndWhoRecordedIt()
    {
        await NotifyAsync();

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("legal@asociacion.test");
        message.ToName.Should().Be("Asociación");
        message
            .TextBody.Should()
            .Contain("«Autorización de imagen» de Leo Ruiz para el evento «Feria de verano»")
            .And.Contain("Lo registró Marta Ruiz el 04/07/2026, 10:30 h.");
    }

    [Fact]
    public async Task HandleAsyncWithoutALegalContactSendsNothing()
    {
        legalEntity.Email = " ";

        await NotifyAsync();

        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncDecisionGoneSendsNothing()
    {
        store.EventTermsAcceptances.Clear();

        await NotifyAsync();

        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncFailedDeliveryIsLoggedWithoutPersonalDataAndNeverThrown()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("SMTP down");

        await NotifyAsync();

        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Sending a ConsentWithdrawalNotice email failed")
            .And.NotContain("Leo")
            .And.NotContain("legal@asociacion.test");
    }
}
