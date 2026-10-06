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
    private readonly ManualEmailOptions options = new();
    private readonly SendEmailToUserCommandHandler sut;

    public SendEmailToUserCommandHandlerTests()
    {
        sut = new SendEmailToUserCommandHandler(
            store,
            new FakeQueryExecutor(),
            NewDispatcher(outbox, options)
        );
    }

    [Fact]
    public async Task HandleAsyncUserWithoutEmailReturnsRecipientWithoutAddress()
    {
        var parent = NewUserRow("Marta", "marta@test.local");
        var child = NewUserRow("Mateo", null, parent);
        store.Users.AddRange([parent, child]);

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(UserId.From(child.Id), Request(), []),
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
            new SendEmailToUserCommand(UserId.New(), Request(), []),
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
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Queued.Should().Be(1);
        var message = outbox.Messages.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.local");
        message.ToName.Should().Be("Ana");
        message.TextBody.Should().Contain("Nos vemos el sábado");
    }
}
