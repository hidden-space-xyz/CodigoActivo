using AwesomeAssertions;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Commands;

public sealed class SendEmailToUserCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ManualEmailOptions options = new();
    private readonly SendEmailToUserCommandHandler sut;

    public SendEmailToUserCommandHandlerTests()
    {
        sut = new SendEmailToUserCommandHandler(
            users,
            new FakeQueryExecutor(),
            NewDispatcher(emailSender, options)
        );
    }

    [Fact]
    public async Task HandleAsyncUserWithoutEmailReturnsRecipientWithoutAddress()
    {
        var parent = NewUser("Marta", "marta@test.local");
        var child = NewUser("Mateo", null, parent);
        users.HasUsers(parent, child);

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(child.Id, Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailRecipientWithoutAddress);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncUnknownUserReturnsNotFound()
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(Guid.NewGuid(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task HandleAsyncKnownUserSendsExactlyOneMessage()
    {
        var ana = NewUser("Ana", "ana@test.local");
        users.HasUsers(ana, NewUser("Berto", "berto@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUserCommand(ana.Id, Request(body: "Nos vemos el sábado"), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(1);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.local");
        message.ToName.Should().Be("Ana");
        message.TextBody.Should().Contain("Nos vemos el sábado");
    }
}
