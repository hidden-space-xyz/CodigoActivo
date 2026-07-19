using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class FileServiceTests
{
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ILocalFileSystemRepository storage =
        Substitute.For<ILocalFileSystemRepository>();
    private readonly TestClock clock = new();
    private readonly FileStorageOptions options = new();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly FileService sut;

    public FileServiceTests()
    {
        sut = new FileService(
            files,
            uow,
            storage,
            clock,
            options,
            NullLogger<FileService>.Instance,
            cacheInvalidator
        );
    }

    private static byte[] PngBytes()
    {
        var bytes = new byte[32];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        bytes[8] = 0x00;
        bytes[9] = 0x00;
        bytes[10] = 0x00;
        bytes[11] = 0x0D;
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        bytes[19] = 0x01;
        bytes[23] = 0x01;
        return bytes;
    }

    private static MemoryStream PngStream() => new(PngBytes(), writable: false);

    private static MemoryStream JunkStream() => new(new byte[32], writable: false);

    private void FileFound(FileEntity file)
    {
        files
            .FindAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(file);
        files
            .GetAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([file]);
    }

    private void FileMissing()
    {
        files
            .FindAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((FileEntity?)null);
        files
            .GetAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private void FileReferenced(bool referenced) =>
        files.IsInUseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(referenced);

    private static FileEntity NewFile(string name = "photo.png", string extension = "png") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Extension = extension,
            UploadedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UploadedBy = Guid.NewGuid(),
        };

    [Fact]
    public async Task GetContentAsync_MetadataMissing_ReturnsNotFound()
    {
        FileMissing();

        var result = await sut.GetContentAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.FileNotFound);
        await storage
            .DidNotReceiveWithAnyArgs()
            .OpenReadAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetContentAsync_KnownSignature_ReturnsDetectedContentTypeAndRewindsStream()
    {
        var file = NewFile(name: "avatar.png");
        FileFound(file);
        var stream = PngStream();
        storage.OpenReadAsync($"{file.Id}.png", Arg.Any<CancellationToken>()).Returns(stream);

        var result = await sut.GetContentAsync(file.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("image/png");
        result.Value.FileName.Should().Be("avatar.png");
        result.Value.Content.Should().BeSameAs(stream);
        result.Value.Content.Position.Should().Be(0);
    }

    [Fact]
    public async Task GetContentAsync_UnknownBytes_FallsBackToOctetStream()
    {
        var file = NewFile();
        FileFound(file);
        storage
            .OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(JunkStream());

        var result = await sut.GetContentAsync(file.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task CreateAsync_UploadMissing_ReturnsValidationError()
    {
        var result = await sut.CreateAsync(
            null,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.FileUploadMissing);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task CreateAsync_UploadEmpty_ReturnsValidationError()
    {
        var upload = new FileUploadRequest(new MemoryStream(), "empty.png", 0);

        var result = await sut.CreateAsync(
            upload,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.FileUploadEmpty);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task CreateAsync_UploadTooLarge_ReturnsValidationError()
    {
        options.MaxSizeBytes = 10;
        var upload = new FileUploadRequest(PngStream(), "big.png", 11);

        var result = await sut.CreateAsync(
            upload,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.FileUploadTooLarge);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task CreateAsync_UploadAtExactSizeLimit_IsAccepted()
    {
        options.MaxSizeBytes = 32;
        var content = PngStream();
        var upload = new FileUploadRequest(content, "exact.png", 32);

        var result = await sut.CreateAsync(
            upload,
            Guid.NewGuid(),
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
    public async Task CreateAsync_StreamNotSeekable_ReturnsValidationError()
    {
        var upload = new FileUploadRequest(new NonSeekableStream(PngBytes()), "x.png", 32);

        var result = await sut.CreateAsync(
            upload,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.FileUploadStreamNotSeekable);
        await AssertNothingPersisted();
    }

    [Fact]
    public async Task CreateAsync_FormatUnsupported_ReturnsValidationError()
    {
        var upload = new FileUploadRequest(JunkStream(), "junk.bin", 32);

        var result = await sut.CreateAsync(
            upload,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.FileUploadUnsupportedFormat);
        await AssertNothingPersisted();
        await storage
            .DidNotReceiveWithAnyArgs()
            .SaveAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateAsync_ValidUpload_SavesContentPersistsEntityAndReturnsResponse()
    {
        var caller = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var content = PngStream();
        var upload = new FileUploadRequest(content, "  C:\\folder\\avatar.png  ", 32);

        var result = await sut.CreateAsync(upload, caller, TestContext.Current.CancellationToken);

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
                Arg.Is<FileEntity>(f =>
                    f.Name == "avatar.png"
                    && f.Extension == "png"
                    && f.UploadedBy == caller
                    && f.UploadedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_BlankFilename_DefaultsNameToFile()
    {
        var upload = new FileUploadRequest(PngStream(), "   ", 32);

        var result = await sut.CreateAsync(
            upload,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("file");
    }

    [Fact]
    public async Task CreateAsync_FileNameLongerThanMaxLength_TruncatesNameTo260Chars()
    {
        var longName = new string('a', 300);
        var upload = new FileUploadRequest(PngStream(), longName, 32);

        var result = await sut.CreateAsync(
            upload,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().HaveLength(260);
        result.Value.Name.Should().Be(new string('a', 260));
    }

    [Fact]
    public async Task CreateAsync_PersistenceThrows_RollsBackStorage()
    {
        var upload = new FileUploadRequest(PngStream(), "avatar.png", 32);
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.CreateAsync(upload, Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Received(1).Delete(Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateAsync_FileMissing_ReturnsNotFound()
    {
        FileMissing();
        var upload = new FileUploadRequest(PngStream(), "new.png", 32);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            upload,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.FileNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await storage
            .DidNotReceiveWithAnyArgs()
            .SaveAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_UploadMissing_ReturnsValidationError()
    {
        FileFound(NewFile());

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            null,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.FileUploadMissing);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await storage
            .DidNotReceiveWithAnyArgs()
            .SaveAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ExtensionUnchanged_ReplacesContentWithoutDeleting()
    {
        var file = NewFile(name: "old.png", extension: "png");
        FileFound(file);
        var content = PngStream();
        var upload = new FileUploadRequest(content, "renamed.png", 32);

        var result = await sut.UpdateAsync(file.Id, upload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("renamed.png");
        file.Name.Should().Be("renamed.png");
        file.Extension.Should().Be("png");
        await storage
            .Received(1)
            .SaveAsync($"{file.Id}.png", content, Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact]
    public async Task UpdateAsync_ValidUpload_InvalidatesFilesCache()
    {
        var file = NewFile(name: "old.png", extension: "png");
        FileFound(file);
        var upload = new FileUploadRequest(PngStream(), "renamed.png", 32);

        var result = await sut.UpdateAsync(file.Id, upload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Files))
            );
    }

    [Fact]
    public async Task UpdateAsync_ExtensionChanges_DeletesOldStoredFile()
    {
        var file = NewFile(name: "old.jpg", extension: "jpg");
        FileFound(file);
        var upload = new FileUploadRequest(PngStream(), "new.png", 32);

        var result = await sut.UpdateAsync(file.Id, upload, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        file.Extension.Should().Be("png");
        await storage
            .Received(1)
            .SaveAsync($"{file.Id}.png", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{file.Id}.jpg");
    }

    [Fact]
    public async Task UpdateAsync_PersistenceThrowsWithExtensionChanged_RollsBackNewContent()
    {
        var file = NewFile(name: "old.jpg", extension: "jpg");
        FileFound(file);
        var upload = new FileUploadRequest(PngStream(), "new.png", 32);
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.UpdateAsync(file.Id, upload, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Received(1).Delete($"{file.Id}.png");
        storage.DidNotReceive().Delete($"{file.Id}.jpg");
    }

    [Fact]
    public async Task UpdateAsync_PersistenceThrowsWithExtensionUnchanged_DoesNotDeleteStoredContent()
    {
        var file = NewFile(name: "old.png", extension: "png");
        FileFound(file);
        var upload = new FileUploadRequest(PngStream(), "new.png", 32);
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.UpdateAsync(file.Id, upload, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact]
    public async Task DeleteAsync_FileMissing_ReturnsNotFound()
    {
        FileMissing();

        var result = await sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.FileNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact]
    public async Task DeleteAsync_FileStillInUse_ReturnsConflict()
    {
        var file = NewFile();
        FileFound(file);
        FileReferenced(true);

        var result = await sut.DeleteAsync(file.Id, TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.FileInUse);
        files.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task DeleteAsync_NotInUse_RemovesRowSavesAndDeletesStoredContent()
    {
        var file = NewFile(name: "gone.png", extension: "png");
        FileFound(file);
        FileReferenced(false);

        var result = await sut.DeleteAsync(file.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        files.Received(1).Remove(file);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{file.Id}.png");
    }

    [Fact]
    public async Task DeleteAsync_NotInUse_InvalidatesFilesCache()
    {
        var file = NewFile(name: "gone.png", extension: "png");
        FileFound(file);
        FileReferenced(false);

        var result = await sut.DeleteAsync(file.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Files))
            );
    }

    [Fact]
    public async Task DeleteIfOrphanedAsync_NoLongerReferenced_DeletesFile()
    {
        var file = NewFile();
        FileFound(file);
        FileReferenced(false);

        await sut.DeleteIfOrphanedAsync(file.Id, TestContext.Current.CancellationToken);

        files.Received(1).Remove(file);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{file.Id}.png");
    }

    [Fact]
    public async Task DeleteIfOrphanedAsync_StillInUse_KeepsFileSilently()
    {
        var file = NewFile();
        FileFound(file);
        FileReferenced(true);

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(file.Id, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        files.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact]
    public async Task DeleteIfOrphanedAsync_FileMissing_IgnoresSilently()
    {
        FileMissing();

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact]
    public async Task DeleteIfOrphanedAsync_StorageThrows_SwallowsException()
    {
        var file = NewFile();
        FileFound(file);
        FileReferenced(false);
        storage.When(s => s.Delete(Arg.Any<string>())).Do(_ => throw new IOException("locked"));

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(file.Id, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteIfOrphanedAsync_Cancelled_PropagatesCancellation()
    {
        files
            .When(f =>
                f.FindAsync(
                    Arg.Any<Expression<Func<FileEntity, bool>>>(),
                    Arg.Any<CancellationToken>()
                )
            )
            .Do(_ => throw new OperationCanceledException());

        var act = async () =>
            await sut.DeleteIfOrphanedAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private void InUseFilesAre(params Guid[] inUse) =>
        files
            .GetInUseAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(inUse.ToList());

    private void StoredFilesAre(params FileEntity[] all) =>
        files
            .GetAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
                all.Where(ci.Arg<Expression<Func<FileEntity, bool>>>().Compile().Invoke).ToList()
            );

    [Fact]
    public async Task DeleteOrphanedAsync_MixedCandidates_RemovesOrphansOnceAndDeletesStoredContent()
    {
        var inUseFile = NewFile(name: "used.png", extension: "png");
        var orphanPng = NewFile(name: "a.png", extension: "png");
        var orphanJpg = NewFile(name: "b.jpg", extension: "jpg");
        InUseFilesAre(inUseFile.Id);
        StoredFilesAre(inUseFile, orphanPng, orphanJpg);

        await sut.DeleteOrphanedAsync(
            [inUseFile.Id, orphanPng.Id, orphanJpg.Id, orphanPng.Id],
            TestContext.Current.CancellationToken
        );

        await files
            .Received(1)
            .GetInUseAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 3),
                Arg.Any<CancellationToken>()
            );
        files.Received(1).Remove(orphanPng);
        files.Received(1).Remove(orphanJpg);
        files.DidNotReceive().Remove(inUseFile);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{orphanPng.Id}.png");
        storage.Received(1).Delete($"{orphanJpg.Id}.jpg");
        storage.DidNotReceive().Delete($"{inUseFile.Id}.png");
    }

    [Fact]
    public async Task DeleteOrphanedAsync_EmptyCandidates_DoesNotTouchRepository()
    {
        await sut.DeleteOrphanedAsync([], TestContext.Current.CancellationToken);

        await files
            .DidNotReceiveWithAnyArgs()
            .GetInUseAsync(default!, TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact]
    public async Task DeleteOrphanedAsync_AllCandidatesInUse_KeepsFiles()
    {
        var first = NewFile();
        var second = NewFile();
        InUseFilesAre(first.Id, second.Id);

        await sut.DeleteOrphanedAsync([first.Id, second.Id], TestContext.Current.CancellationToken);

        files.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        storage.DidNotReceiveWithAnyArgs().Delete(default!);
    }

    [Fact]
    public async Task DeleteOrphanedAsync_RepositoryThrows_SwallowsException()
    {
        files
            .When(f =>
                f.GetInUseAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            )
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.DeleteOrphanedAsync([Guid.NewGuid()], TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteOrphanedAsync_StorageThrows_SwallowsExceptionAfterRemoving()
    {
        var orphan = NewFile();
        InUseFilesAre();
        StoredFilesAre(orphan);
        storage.When(s => s.Delete(Arg.Any<string>())).Do(_ => throw new IOException("locked"));

        var act = async () =>
            await sut.DeleteOrphanedAsync([orphan.Id], TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        files.Received(1).Remove(orphan);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteOrphanedAsync_Cancelled_PropagatesCancellation()
    {
        files
            .When(f =>
                f.GetInUseAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            )
            .Do(_ => throw new OperationCanceledException());

        var act = async () =>
            await sut.DeleteOrphanedAsync([Guid.NewGuid()], TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private async Task AssertNothingPersisted()
    {
        await files.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
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

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
