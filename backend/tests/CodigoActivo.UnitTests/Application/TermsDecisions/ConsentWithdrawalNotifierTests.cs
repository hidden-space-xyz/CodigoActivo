using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Legal;
using CodigoActivo.Application.TermsDecisions;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using TermsDocumentKind = CodigoActivo.Application.Abstractions.Querying.ReadModel.TermsDocumentKind;

namespace CodigoActivo.UnitTests.Application.TermsDecisions;

public sealed class ConsentWithdrawalNotifierTests
{
    private static readonly DateOnly Today = new(2026, 7, 4);
    private static readonly DateTimeOffset GivenAt = new(2026, 6, 20, 9, 15, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly RecordingEmailSender emailSender = new();
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
    private readonly EventRow fair = new()
    {
        Id = Guid.NewGuid(),
        Title = "Feria de verano",
        EventStartsAt = Today.AddDays(-3),
    };
    private readonly EventRow camp = new()
    {
        Id = Guid.NewGuid(),
        Title = "Campamento",
        EventStartsAt = Today.AddDays(10),
    };
    private readonly TermsDocumentRow image = new()
    {
        Id = Guid.NewGuid(),
        Name = "Autorización de imagen",
        Kind = TermsDocumentKind.Consent,
    };
    private readonly TermsDocumentRow rules = new()
    {
        Id = Guid.NewGuid(),
        Name = "Normas",
        Kind = TermsDocumentKind.Conditions,
    };

    public ConsentWithdrawalNotifierTests()
    {
        store.Users.AddRange(leo, marta);
    }

    private EventTermsAcceptance Accepted(EventRow ev, TermsDocumentRow document)
    {
        store.EventTermsAcceptances.Add(
            new EventTermsAcceptanceRow
            {
                EventId = ev.Id,
                Event = ev,
                UserId = leo.Id,
                User = leo,
                DecidedById = marta.Id,
                DecidedBy = marta,
                TermsDocumentId = document.Id,
                TermsDocument = document,
                Accepted = true,
                DecidedAt = GivenAt,
            }
        );
        return EventTermsAcceptance.Record(
            EventId.From(ev.Id),
            UserId.From(leo.Id),
            UserId.From(marta.Id),
            TermsDocumentId.From(document.Id),
            1,
            accepted: true,
            signature: null,
            UserId.From(marta.Id),
            GivenAt
        );
    }

    private Task<int> NotifyErasedAsync(params EventTermsAcceptance[] accepted)
    {
        var notifier = new ConsentWithdrawalNotifier(
            store,
            new FakeQueryExecutor(),
            legalEntity,
            emailSender,
            new ConsentWithdrawalEmailComposer(
                new ApplicationOptions { BaseUrl = "https://app.test/" },
                new TestClock()
            ),
            new TestClock(today: Today)
        );
        return notifier.NotifyErasedAsync(accepted, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NotifyErasedAsyncConsentOfAStartedEventTellsTheLegalContactWhoGaveIt()
    {
        var told = await NotifyErasedAsync(Accepted(fair, image));

        told.Should().Be(1);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.ConsentWithdrawalNotice);
        message.ToAddress.Should().Be("legal@asociacion.test");
        message.Topic.Should().BeNull("a new notice never replaces one still waiting");
        message.Subject.Should().Be("Ya no consta un consentimiento");
        message
            .TextBody.Should()
            .Contain("«Autorización de imagen» de Leo Ruiz para el evento «Feria de verano»")
            .And.Contain("Lo dio Marta Ruiz el 20/06/2026, 09:15 h.");
    }

    [Fact]
    public async Task NotifyErasedAsyncLeavesOutConditionsAndEventsThatHaveNotStarted()
    {
        var told = await NotifyErasedAsync(
            Accepted(fair, rules),
            Accepted(camp, image),
            Accepted(fair, image)
        );

        told.Should().Be(1);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.TextBody.Should().Contain("«Feria de verano»").And.NotContain("«Campamento»");
        message.TextBody.Should().NotContain("«Normas»");
    }

    [Fact]
    public async Task NotifyErasedAsyncEventStartingTodayIsTold()
    {
        var today = new EventRow
        {
            Id = Guid.NewGuid(),
            Title = "Jornada",
            EventStartsAt = Today,
        };

        var told = await NotifyErasedAsync(Accepted(today, image));

        told.Should().Be(1);
    }

    [Fact]
    public async Task NotifyErasedAsyncNothingToTellSendsNothing()
    {
        var told = await NotifyErasedAsync(Accepted(camp, image), Accepted(fair, rules));

        told.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyErasedAsyncWithoutALegalContactSendsNothing()
    {
        legalEntity.Email = " ";

        var told = await NotifyErasedAsync(Accepted(fair, image));

        told.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyErasedAsyncSplitsTheConsentsIntoNoticesOfTheMaximumSize()
    {
        var accepted = Enumerable
            .Range(0, ConsentWithdrawalNotifier.MaxPerNotice + 1)
            .Select(index =>
                Accepted(
                    fair,
                    new TermsDocumentRow
                    {
                        Id = Guid.NewGuid(),
                        Name = $"Consentimiento {index}",
                        Kind = TermsDocumentKind.Consent,
                    }
                )
            )
            .ToArray();

        var told = await NotifyErasedAsync(accepted);

        told.Should().Be(ConsentWithdrawalNotifier.MaxPerNotice + 1);
        emailSender
            .Sent.Select(message => message.Subject)
            .Should()
            .Equal(
                $"Ya no constan {ConsentWithdrawalNotifier.MaxPerNotice} consentimientos",
                "Ya no consta un consentimiento"
            );
    }
}
