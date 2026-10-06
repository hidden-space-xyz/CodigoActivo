using System.Text;
using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Commands;

public sealed class SendEmailToUsersCommandHandlerTests : IDisposable
{
    private readonly List<MemoryStream> attachmentStreams = [];
    private readonly FakeReadStore store = new();
    private readonly RecordingEmailOutbox outbox = new();
    private readonly ManualEmailOptions options = new();
    private readonly SendEmailToUsersCommandHandler sut;

    public SendEmailToUsersCommandHandlerTests()
    {
        sut = new SendEmailToUsersCommandHandler(
            store,
            new FakeQueryExecutor(),
            options,
            NewDispatcher(outbox, options)
        );
    }

    private EmailAttachmentUpload Attachment(string name = "acta.pdf", int size = 4)
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', size)));
        attachmentStreams.Add(content);
        return new(content, name, "text/plain", size);
    }

    public void Dispose()
    {
        foreach (var stream in attachmentStreams)
        {
            stream.Dispose();
        }
    }

    [Fact]
    public async Task HandleAsyncSeveralRecipientsQueuesOneMessagePerRecipientInOneBatch()
    {
        store.Users.AddRange([
            NewUserRow("Ana", "ana@test.local"),
            NewUserRow("Berto", "berto@test.local"),
        ]);

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Queued.Should().Be(2);
        outbox
            .Batches.Should()
            .ContainSingle("the whole batch is queued as one shared content")
            .Which.Recipients.Select(r => r.Address)
            .Should()
            .BeEquivalentTo("ana@test.local", "berto@test.local");
        outbox.Messages.Should().OnlyContain(m => m.Subject == "Asunto");
    }

    [Fact]
    public async Task HandleAsyncDependentWithoutEmailSkipsItWithoutSending()
    {
        var parent = NewUserRow("Marta", "marta@test.local");
        store.Users.AddRange([parent, NewUserRow("Mateo", null, parent)]);

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Queued.Should().Be(1);
        result.Value.Skipped.Should().Be(1);
        outbox.Messages.Should().ContainSingle().Which.ToAddress.Should().Be("marta@test.local");
    }

    [Fact]
    public async Task HandleAsyncFilterMatchesOnlyDependentsReturnsNoRecipients()
    {
        var parent = NewUserRow("Marta", "marta@test.local");
        store.Users.AddRange([parent, NewUserRow("Mateo", null, parent)]);

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery { ParentId = parent.Id }, Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailNoRecipients);
        outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncNameFilterOnlyMailsMatchingUsers()
    {
        store.Users.AddRange([
            NewUserRow("Ana", "ana@test.local"),
            NewUserRow("Berto", "berto@test.local"),
        ]);

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery { Name = "berto" }, Request(), []),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        outbox.Messages.Should().ContainSingle().Which.ToAddress.Should().Be("berto@test.local");
    }

    [Fact]
    public async Task HandleAsyncMoreRecipientsThanAllowedReturnsTooManyRecipients()
    {
        options.MaxRecipients = 1;
        store.Users.AddRange([
            NewUserRow("Ana", "ana@test.local"),
            NewUserRow("Berto", "berto@test.local"),
        ]);

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailTooManyRecipients);
        outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncOutboxWithoutRoomReturnsSendFailedAndQueuesNothing()
    {
        store.Users.AddRange([
            NewUserRow("Ana", "ana@test.local"),
            NewUserRow("Berto", "berto@test.local"),
        ]);
        outbox.RejectAll = true;

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailSendFailed);
        outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncOutboxWriteFailsReturnsSendFailed()
    {
        store.Users.Add(NewUserRow("Ana", "ana@test.local"));
        outbox.ThrowOnEnqueue = new InvalidOperationException("database down");

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), []),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailSendFailed);
    }

    [Fact]
    public async Task HandleAsyncWithAttachmentSharesOneCopyForEveryRecipient()
    {
        store.Users.AddRange([
            NewUserRow("Ana", "ana@test.local"),
            NewUserRow("Berto", "berto@test.local"),
        ]);

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), [Attachment(size: 6)]),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var batch = outbox.Batches.Should().ContainSingle().Subject;
        batch.Recipients.Should().HaveCount(2);
        batch
            .Attachments.Should()
            .ContainSingle()
            .Which.Content.Length.Should()
            .Be(6, "the bytes are stored once for the whole batch");
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData(@"..\..\etc\passwd")]
    public async Task HandleAsyncAttachmentPathInFileNameKeepsOnlyTheFileName(string fileName)
    {
        store.Users.Add(NewUserRow("Ana", "ana@test.local"));

        await sut.HandleAsync(
            new SendEmailToUsersCommand(
                new UserListQuery(),
                Request(),
                [Attachment(name: fileName)]
            ),
            TestContext.Current.CancellationToken
        );

        outbox.Messages[0].Attachments![0].FileName.Should().Be("passwd");
    }

    [Fact]
    public async Task HandleAsyncAttachmentsOverTheSizeCapReturnsAttachmentsTooLarge()
    {
        options.MaxAttachmentsBytes = 4;
        store.Users.Add(NewUserRow("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), [Attachment(size: 5)]),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailAttachmentsTooLarge);
        outbox.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncMoreAttachmentsThanAllowedReturnsTooManyAttachments()
    {
        options.MaxAttachments = 1;
        store.Users.Add(NewUserRow("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(
                new UserListQuery(),
                Request(),
                [Attachment(), Attachment("otro.pdf")]
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailTooManyAttachments);
    }

    [Fact]
    public async Task HandleAsyncEmptyAttachmentReturnsAttachmentEmpty()
    {
        store.Users.Add(NewUserRow("Ana", "ana@test.local"));

        var result = await sut.HandleAsync(
            new SendEmailToUsersCommand(new UserListQuery(), Request(), [Attachment(size: 0)]),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.EmailAttachmentEmpty);
    }
}
