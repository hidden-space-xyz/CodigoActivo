using System.Text;
using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Commands;

public sealed class SendEmailToUsersCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ManualEmailOptions options = new();
    private readonly SendEmailToUsersCommandHandler sut;

    public SendEmailToUsersCommandHandlerTests()
    {
        sut = new SendEmailToUsersCommandHandler(
            users,
            new FakeQueryExecutor(),
            options,
            NewDispatcher(emailSender, options)
        );
    }

    private static EmailAttachmentUpload Attachment(string name = "acta.pdf", int size = 4)
    {
        return new(
            new MemoryStream(Encoding.UTF8.GetBytes(new string('x', size))),
            name,
            "text/plain",
            size
        );
    }

    [Fact]
    public async Task HandleAsyncSeveralRecipientsSendsOneMessagePerRecipientInOneBatch()
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(2);
        emailSender.Batches.Should().Be(1, "every recipient shares one SMTP connection");
        emailSender
            .Sent.Select(m => m.ToAddress)
            .Should()
            .BeEquivalentTo("ana@test.local", "berto@test.local");
        emailSender.Sent.Should().OnlyContain(m => m.Subject == "Asunto");
    }

    [Fact]
    public async Task HandleAsyncDependentWithoutEmailSkipsItWithoutSending()
    {
        var parent = NewUser("Marta", "marta@test.local");
        users.HasUsers(parent, NewUser("Mateo", null, parent));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(1);
        result.Value.Skipped.Should().Be(1);
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("marta@test.local");
    }

    [Fact]
    public async Task HandleAsyncFilterMatchesOnlyDependentsReturnsNoRecipients()
    {
        var parent = NewUser("Marta", "marta@test.local");
        users.HasUsers(parent, NewUser("Mateo", null, parent));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery { ParentId = parent.Id }, Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailNoRecipients);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncNameFilterOnlyMailsMatchingUsers()
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery { Name = "berto" }, Request(), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("berto@test.local");
    }

    [Fact]
    public async Task HandleAsyncMoreRecipientsThanAllowedReturnsTooManyRecipients()
    {
        options.MaxRecipients = 1;
        users.HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailTooManyRecipients);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncSomeRecipientsRejectedReportsThemAsFailed()
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));
        emailSender.FailingRecipients.Add("berto@test.local");

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(1);
        result.Value.Failed.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsyncSmtpUnavailableReturnsSendFailed()
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"));
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailSendFailed);
    }

    [Fact]
    public async Task HandleAsyncWithAttachmentBuffersItForEveryRecipient()
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), [Attachment(size: 6)]),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender
            .Sent.Should()
            .OnlyContain(m => m.Attachments!.Count == 1 && m.Attachments[0].Content.Length == 6);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData(@"..\..\etc\passwd")]
    public async Task HandleAsyncAttachmentPathInFileNameKeepsOnlyTheFileName(string fileName)
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"));

        await sut.HandleAsync(
            new SendEmailToUsersCommand(
                new UserListQuery(),
                Request(),
                [Attachment(name: fileName)]
            ),
            TestContext.Current.CancellationToken
        );

        emailSender.Sent[0].Attachments![0].FileName.Should().Be("passwd");
    }

    [Fact]
    public async Task HandleAsyncAttachmentsOverTheSizeCapReturnsAttachmentsTooLarge()
    {
        options.MaxAttachmentsBytes = 4;
        users.HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), [Attachment(size: 5)]),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailAttachmentsTooLarge);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncMoreAttachmentsThanAllowedReturnsTooManyAttachments()
    {
        options.MaxAttachments = 1;
        users.HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(
                new UserListQuery(),
                Request(),
                [Attachment(), Attachment("otro.pdf")]
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailTooManyAttachments);
    }

    [Fact]
    public async Task HandleAsyncEmptyAttachmentReturnsAttachmentEmpty()
    {
        users.HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), [Attachment(size: 0)]),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailAttachmentEmpty);
    }
}
