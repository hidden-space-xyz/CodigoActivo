using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.BlockedData;

public sealed class SendBlockedDataConsultationNoticesCommandHandlerTests
{
    private static readonly DateTimeOffset OpenedAt = new(2026, 7, 4, 10, 30, 0, TimeSpan.Zero);

    private readonly FakeReadStore store = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly IBlockedDataConsultationRepository consultations =
        Substitute.For<IBlockedDataConsultationRepository>();
    private readonly IPendingEmails pendingEmails = Substitute.For<IPendingEmails>();
    private readonly LegalEntityOptions legalEntity = new()
    {
        Name = "Asociación",
        Email = "legal@asociacion.test",
    };
    private readonly UserRow officer = new()
    {
        Id = Guid.NewGuid(),
        FirstName = "Olga",
        LastName = "Martín",
    };

    public SendBlockedDataConsultationNoticesCommandHandlerTests()
    {
        store.Users.Add(officer);
    }

    private BlockedDataConsultation Opened(string reference)
    {
        var consultation = BlockedDataConsultation
            .Open(UserId.From(officer.Id), BlockedDataAuthority.Court, reference, OpenedAt)
            .Value;
        store.BlockedDataConsultations.Add(
            new BlockedDataConsultationRow
            {
                Id = consultation.Id.Value,
                OfficerId = officer.Id,
                Authority = BlockedDataAuthority.Court,
                Reference = reference,
                OpenedAt = OpenedAt,
                ExpiresAt = consultation.ExpiresAt,
            }
        );
        return consultation;
    }

    private void Pending(params BlockedDataConsultation[] pending)
    {
        consultations
            .ListNoticesPendingAsync(
                SendBlockedDataConsultationNoticesCommandHandler.MaxPerNotice,
                Arg.Any<CancellationToken>()
            )
            .Returns(pending);
    }

    private async Task<int> HandleAsync()
    {
        var handler = new SendBlockedDataConsultationNoticesCommandHandler(
            consultations,
            store,
            new FakeQueryExecutor(),
            legalEntity,
            pendingEmails,
            emailSender,
            new BlockedDataEmailComposer(
                new ApplicationOptions { BaseUrl = "https://app.test/" },
                new TestClock()
            )
        );
        var result = await handler.HandleAsync(
            new SendBlockedDataConsultationNoticesCommand(),
            TestContext.Current.CancellationToken
        );
        return result.Value;
    }

    [Fact]
    public async Task HandleAsyncOneConsultationNamesWhoOpenedItAndRecordsTheNotice()
    {
        var consultation = Opened("DP 12/2026");
        Pending(consultation);

        var told = await HandleAsync();

        told.Should().Be(1);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("legal@asociacion.test");
        message.Topic.Should().BeNull("a new summary never replaces the one still waiting");
        message.Subject.Should().Be("Se ha abierto una consulta de datos bloqueados");
        message.TextBody.Should().Contain("Olga Martín").And.Contain("DP 12/2026");
        consultation.NoticePending.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncSeveralConsultationsGoInOneEmail()
    {
        var first = Opened("DP 12/2026");
        var second = Opened("DP 13/2026");
        Pending(first, second);

        var told = await HandleAsync();

        told.Should().Be(2);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Subject.Should().Be("Se han abierto 2 consultas de datos bloqueados");
        message.TextBody.Should().Contain("DP 12/2026").And.Contain("DP 13/2026");
        first.NoticePending.Should().BeFalse();
        second.NoticePending.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsyncPreviousNoticeStillWaitingKeepsTheConsultationsForTheNextOne()
    {
        var consultation = Opened("DP 12/2026");
        Pending(consultation);
        pendingEmails
            .AnyAsync(EmailKind.BlockedDataConsultationNotice, Arg.Any<CancellationToken>())
            .Returns(true);

        var told = await HandleAsync();

        told.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        consultation.NoticePending.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsyncWithoutALegalContactSendsNothingAndKeepsThemWaiting()
    {
        var consultation = Opened("DP 12/2026");
        Pending(consultation);
        legalEntity.Email = string.Empty;

        var told = await HandleAsync();

        told.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        consultation.NoticePending.Should().BeTrue();
    }
}
