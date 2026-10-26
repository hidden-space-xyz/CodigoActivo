using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Commands;

public sealed class CreateFileCommandHandlerTests
{
    private static readonly ProcessedImage Processed = new([1, 2, 3], [4, 5]);

    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage storage = Substitute.For<IFileStorage>();
    private readonly IImageProcessor processor = Substitute.For<IImageProcessor>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly FileUploadOptions options = new();
    private readonly CreateFileCommandHandler sut;

    public CreateFileCommandHandlerTests()
    {
        sut = new CreateFileCommandHandler(
            files,
            uow,
            storage,
            currentUser,
            clock,
            new FileUploadValidator(options),
            processor
        );
        processor.ProcessAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(Processed);
    }

    private async Task<List<StoredFile>> CaptureAddedFilesAsync()
    {
        var added = new List<StoredFile>();
        await files.AddAsync(Arg.Do<StoredFile>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    private List<(string Name, byte[] Content)> CaptureSavedContent()
    {
        var saved = new List<(string Name, byte[] Content)>();
        storage
            .When(s =>
                s.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            )
            .Do(call =>
            {
                using var copy = new MemoryStream();
                call.Arg<Stream>().CopyTo(copy);
                saved.Add((call.Arg<string>(), copy.ToArray()));
            });
        return saved;
    }

    [Fact]
    public async Task HandleAsyncUploadMissingReturnsValidationError()
    {
        var result = await sut.HandleAsync(
            new CreateFileCommand(null),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadMissing);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncUploadEmptyReturnsValidationError()
    {
        using var content = new MemoryStream();
        var upload = new FileUpload(content, "empty.png", 0);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadEmpty);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncUploadTooLargeReturnsValidationError()
    {
        options.MaxSizeBytes = 10;
        var upload = new FileUpload(PngStream(), "big.png", 11);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadTooLarge);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncUploadAtExactSizeLimitIsAccepted()
    {
        options.MaxSizeBytes = 32;
        var upload = new FileUpload(PngStream(), "exact.png", 32);
        var added = await CaptureAddedFilesAsync();

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle().Which.Id.Should().Be(result.Value);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncStreamNotSeekableReturnsValidationError()
    {
        var upload = new FileUpload(new NonSeekableStream(PngBytes()), "x.png", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadStreamNotSeekable);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncFormatUnsupportedReturnsValidationError()
    {
        var upload = new FileUpload(JunkStream(), "junk.bin", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadUnsupportedFormat);
        await AssertNothingPersistedAsync();
        await processor
            .DidNotReceiveWithAnyArgs()
            .ProcessAsync(Stream.Null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncProcessingFailsReturnsItsErrorAndStoresNothing()
    {
        processor
            .ProcessAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Error.Validation(ApplicationErrorCode.FileUploadInvalidImage));
        var upload = new FileUpload(PngStream(), "broken.png", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadInvalidImage);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidUploadStoresTheProcessedVariantsPersistsEntityAndReturnsId()
    {
        var caller = Guid.NewGuid();
        currentUser.Id = UserId.From(caller);
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var content = PngStream();
        var upload = new FileUpload(content, "  C:\\folder\\avatar.png  ", 32);
        var added = await CaptureAddedFilesAsync();
        var saved = CaptureSavedContent();

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Name.Should().Be("avatar.png");
        created.Extension.Should().Be("webp");
        created.UploadedBy.Value.Should().Be(caller);
        created.UploadedAt.Should().Be(clock.UtcNow);

        await processor.Received(1).ProcessAsync(content, Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            storage.SaveAsync(
                $"{created.Id}.webp",
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()
            );
            storage.SaveAsync(
                $"{created.Id}.thumbnail.webp",
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()
            );
            uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        saved
            .Should()
            .SatisfyRespectively(
                full => full.Content.Should().Equal(Processed.Full),
                thumbnail => thumbnail.Content.Should().Equal(Processed.Thumbnail)
            );
    }

    [Fact]
    public async Task HandleAsyncBlankFilenameDefaultsNameToFile()
    {
        var upload = new FileUpload(PngStream(), "   ", 32);
        var added = await CaptureAddedFilesAsync();

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle().Which.Name.Should().Be("file");
    }

    [Fact]
    public async Task HandleAsyncFileNameLongerThanMaxLengthTruncatesNameTo260Chars()
    {
        var longName = new string('a', 300);
        var upload = new FileUpload(PngStream(), longName, 32);
        var added = await CaptureAddedFilesAsync();

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var name = added.Should().ContainSingle().Which.Name;
        name.Should().HaveLength(260);
        name.Should().Be(new string('a', 260));
    }

    [Fact]
    public async Task HandleAsyncFileNameWithControlOrFormatCharactersDropsThem()
    {
        var upload = new FileUpload(PngStream(), "fac\u0000tura\r\n‮gnp.exe", 32);
        var added = await CaptureAddedFilesAsync();

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle().Which.Name.Should().Be("facturagnp.exe");
    }

    [Fact]
    public async Task HandleAsyncPersistenceThrowsRemovesBothStoredVariants()
    {
        var upload = new FileUpload(PngStream(), "avatar.png", 32);
        var added = await CaptureAddedFilesAsync();
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.HandleAsync(
                new CreateFileCommand(upload),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        var id = added.Should().ContainSingle().Which.Id;
        storage.Received(1).TryDelete($"{id}.webp");
        storage.Received(1).TryDelete($"{id}.thumbnail.webp");
    }

    [Fact]
    public async Task HandleAsyncSecondVariantFailsToSaveRemovesTheFirstAndPersistsNothing()
    {
        var upload = new FileUpload(PngStream(), "avatar.png", 32);
        storage
            .SaveAsync(
                Arg.Is<string>(name => name.Contains(".thumbnail.", StringComparison.Ordinal)),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromException(new IOException("disk full")));

        var act = async () =>
            await sut.HandleAsync(
                new CreateFileCommand(upload),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<IOException>();
        storage
            .Received(1)
            .TryDelete(
                Arg.Is<string>(name => !name.Contains(".thumbnail.", StringComparison.Ordinal))
            );
        storage
            .DidNotReceive()
            .TryDelete(
                Arg.Is<string>(name => name.Contains(".thumbnail.", StringComparison.Ordinal))
            );
        await AssertNothingPersistedAsync();
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AssertNothingPersistedAsync()
    {
        await files.DidNotReceiveWithAnyArgs().AddAsync(NewFile(), default);
        await AssertNotSavedAsync();
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
