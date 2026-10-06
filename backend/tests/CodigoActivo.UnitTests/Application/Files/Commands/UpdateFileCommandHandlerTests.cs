using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Files.FileTestData;

namespace CodigoActivo.UnitTests.Application.Files.Commands;

public sealed class UpdateFileCommandHandlerTests
{
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage storage = Substitute.For<IFileStorage>();
    private readonly TestClock clock = new();
    private readonly FileUploadOptions options = new();
    private readonly UpdateFileCommandHandler sut;

    public UpdateFileCommandHandlerTests()
    {
        sut = new UpdateFileCommandHandler(
            files,
            uow,
            storage,
            clock,
            new FileUploadValidator(options)
        );
    }

    [Fact]
    public async Task HandleAsyncFileMissingReturnsNotFound()
    {
        files.FileMissing();
        var upload = new FileUpload(PngStream(), "new.png", 32);

        var result = await sut.HandleAsync(
            new UpdateFileCommand(StoredFileId.New(), upload),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.FileNotFound);
        await AssertNotSavedAsync();
        await storage
            .DidNotReceiveWithAnyArgs()
            .SaveAsync(string.Empty, Stream.Null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUploadMissingReturnsValidationError()
    {
        files.FileFound(NewFile());

        var result = await sut.HandleAsync(
            new UpdateFileCommand(StoredFileId.New(), null),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.FileUploadMissing);
        await AssertNotSavedAsync();
        await storage
            .DidNotReceiveWithAnyArgs()
            .SaveAsync(string.Empty, Stream.Null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncExtensionUnchangedReplacesContentWithoutDeletingAndRaisesReplacement()
    {
        var file = NewFile(name: "old.png", extension: "png");
        files.FileFound(file);
        var content = PngStream();
        var upload = new FileUpload(content, "renamed.png", 32);

        var result = await sut.HandleAsync(
            new UpdateFileCommand(file.Id, upload),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        file.Name.Should().Be("renamed.png");
        file.Extension.Should().Be("png");
        await storage
            .Received(1)
            .SaveAsync($"{file.Id}.png", content, Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
        file.PullDomainEvents()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoredFileReplaced>();
    }

    [Fact]
    public async Task HandleAsyncExtensionChangesDeletesOldStoredFile()
    {
        var file = NewFile(name: "old.jpg", extension: "jpg");
        files.FileFound(file);
        var upload = new FileUpload(PngStream(), "new.png", 32);

        var result = await sut.HandleAsync(
            new UpdateFileCommand(file.Id, upload),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        file.Extension.Should().Be("png");
        await storage
            .Received(1)
            .SaveAsync($"{file.Id}.png", Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        storage.Received(1).Delete($"{file.Id}.jpg");
    }

    [Fact]
    public async Task HandleAsyncPersistenceThrowsWithExtensionChangedRollsBackNewContent()
    {
        var file = NewFile(name: "old.jpg", extension: "jpg");
        files.FileFound(file);
        var upload = new FileUpload(PngStream(), "new.png", 32);
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.HandleAsync(
                new UpdateFileCommand(file.Id, upload),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Received(1).Delete($"{file.Id}.png");
        storage.DidNotReceive().Delete($"{file.Id}.jpg");
    }

    [Fact]
    public async Task HandleAsyncPersistenceThrowsWithExtensionUnchangedDoesNotDeleteStoredContent()
    {
        var file = NewFile(name: "old.png", extension: "png");
        files.FileFound(file);
        var upload = new FileUpload(PngStream(), "new.png", 32);
        uow.When(u => u.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("db down"));

        var act = async () =>
            await sut.HandleAsync(
                new UpdateFileCommand(file.Id, upload),
                TestContext.Current.CancellationToken
            );

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.DidNotReceiveWithAnyArgs().Delete(string.Empty);
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
