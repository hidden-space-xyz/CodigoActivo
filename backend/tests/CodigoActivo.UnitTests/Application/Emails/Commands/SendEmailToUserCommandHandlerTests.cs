using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Commands;

public sealed class SendEmailToUserCommandHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly RecordingEmailOutbox outbox = new();
    private readonly FakeManualEmailComposer composer = new();
    private readonly ManualEmailOptions options = new();
    private readonly SendEmailToUserCommandHandler sut;

    public SendEmailToUserCommandHandlerTests()
    {
        sut = new SendEmailToUserCommandHandler(
            store,
            new FakeQueryExecutor(),
            NewDispatcher(outbox, options, composer)
        );
    }

    [Fact]
    public async Task HandleAsyncUserWithoutEmailReturnsRecipientWithoutAddress()
    {
        var parent = NewUserRow("Marta", "marta@test.local");
        var child = NewUserRow("Mateo", null, parent);
        store.Users.AddRange([parent, child]);

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(
                UserId.From(child.Id),
                Request(),
                ManualEmailPurpose.Informational,
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailRecipientWithoutAddress);
        outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncUnknownUserReturnsNotFound()
    {
        store.Users.Add(NewUserRow("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(
                UserId.New(),
                Request(),
                ManualEmailPurpose.Informational,
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncKnownUserSendsExactlyOneMessage()
    {
        var ana = NewUserRow("Ana", "ana@test.local");
        store.Users.AddRange([ana, NewUserRow("Berto", "berto@test.local")]);

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(
                UserId.From(ana.Id),
                Request(body: "Nos vemos el sábado"),
                ManualEmailPurpose.Informational,
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Queued.Should().Be(1);
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Subject.Should().Be("Asunto");
        email.Body.Should().Be("Nos vemos el sábado");
        email.Purpose.Should().Be(ManualEmailPurpose.Informational);
        email
            .Recipients.Should()
            .Equal(new ManualEmailRecipient(UserId.From(ana.Id), "ana@test.local", "Ana"));
        var message = outbox.Messages.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.local");
        message.ToName.Should().Be("Ana");
    }

    [Fact]
    public async Task HandleAsyncPromotionalToUserWithoutConsentReturnsRecipientWithoutConsent()
    {
        var ana = NewUserRow("Ana", "ana@test.local");
        store.Users.Add(ana);

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(
                UserId.From(ana.Id),
                Request(),
                ManualEmailPurpose.Promotional,
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailRecipientWithoutConsent);
        outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncPromotionalToUserWithConsentComposesItAsPromotional()
    {
        var ana = NewUserRow("Ana", "ana@test.local", promotionalConsent: true);
        store.Users.Add(ana);

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(
                UserId.From(ana.Id),
                Request(),
                ManualEmailPurpose.Promotional,
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        composer
            .Composed.Should()
            .ContainSingle()
            .Which.Purpose.Should()
            .Be(ManualEmailPurpose.Promotional);
    }
}
