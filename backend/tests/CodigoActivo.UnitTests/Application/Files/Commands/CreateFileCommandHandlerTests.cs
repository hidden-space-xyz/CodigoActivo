using AwesomeAssertions;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Commands;

public sealed class CreateFileCommandHandlerTests
{
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ILocalFileSystemRepository storage =
        Substitute.For<ILocalFileSystemRepository>();
    private readonly TestClock clock = new();
    private readonly FileUploadOptions options = new();
    private readonly CreateFileCommandHandler sut;

    public CreateFileCommandHandlerTests()
    {
        sut = new CreateFileCommandHandler(
            files,
            uow,
            storage,
            clock,
            new FileUploadValidator(options)
        );
    }

    private static bool IsUploadedAvatar(FileEntity? file, Guid caller, DateTimeOffset uploadedAt)
    {
        if (file is null)
        {
            return false;
        }

        var isAvatarPng =
            string.Equals(file.Name, "avatar.png", StringComparison.Ordinal)
            && string.Equals(file.Extension, "png", StringComparison.Ordinal);
        return isAvatarPng && file.UploadedBy == caller && file.UploadedAt == uploadedAt;
    }

    [Fact]
    public async Task HandleAsyncUploadMissingReturnsValidationError()
    {
        var result = await sut.HandleAsync(
            new CreateFileCommand(null, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.FileUploadMissing);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncUploadEmptyReturnsValidationError()
    {
        var upload = new FileUpload(new MemoryStream(), "empty.png", 0);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.FileUploadEmpty);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncUploadTooLargeReturnsValidationError()
    {
        options.MaxSizeBytes = 10;
        var upload = new FileUpload(PngStream(), "big.png", 11);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.FileUploadTooLarge);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncUploadAtExactSizeLimitIsAccepted()
    {
        options.MaxSizeBytes = 32;
        var content = PngStream();
        var upload = new FileUpload(content, "exact.png", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Extension.Should().Be("png");
        await storage
            .Received(1)
            .SaveAsync($"{result.Value.Id}.png", content, Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncStreamNotSeekableReturnsValidationError()
    {
        var upload = new FileUpload(new NonSeekableStream(PngBytes()), "x.png", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.FileUploadStreamNotSeekable);
        await AssertNothingPersistedAsync();
    }

    [Fact]
    public async Task HandleAsyncFormatUnsupportedReturnsValidationError()
    {
        var upload = new FileUpload(JunkStream(), "junk.bin", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.FileUploadUnsupportedFormat);
        await AssertNothingPersistedAsync();
        await storage
            .DidNotReceiveWithAnyArgs()
            .SaveAsync(string.Empty, Stream.Null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidUploadSavesContentPersistsEntityAndReturnsResponse()
    {
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var content = PngStream();
        var upload = new FileUpload(content, "  C:\\folder\\avatar.png  ", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("avatar.png");
        result.Value.Extension.Should().Be("png");
        result.Value.UploadedBy.Should().Be(caller);
        result.Value.UploadedAt.Should().Be(clock.UtcNow);

        await storage
            .Received(1)
            .SaveAsync($"{result.Value.Id}.png", content, Arg.Any<CancellationToken>());
        await files
            .Received(1)
            .AddAsync(
                Arg.Is<FileEntity>(f => IsUploadedAvatar(f, caller, clock.UtcNow)),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncBlankFilenameDefaultsNameToFile()
    {
        var upload = new FileUpload(PngStream(), "   ", 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("file");
    }

    [Fact]
    public async Task HandleAsyncFileNameLongerThanMaxLengthTruncatesNameTo260Chars()
    {
        var longName = new string('a', 300);
        var upload = new FileUpload(PngStream(), longName, 32);

        var result = await sut.HandleAsync(
            new CreateFileCommand(upload, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().HaveLength(260);
        result.Value.Name.Should().Be(new string('a', 260));
    }

    [Fact]
    public async Task HandleAsyncPersistenceThrowsRollsBackStorage()
    {
        var upload = new FileUpload(PngStream(), "avatar.png", 32);
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.HandleAsync(
                new CreateFileCommand(upload, Guid.NewGuid()),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Received(1).Delete(Arg.Any<string>());
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
