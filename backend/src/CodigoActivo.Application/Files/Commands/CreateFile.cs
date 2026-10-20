using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Storage;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files.Commands;

/// <summary>
/// Carries the input required to create a file.
/// </summary>
/// <param name="Upload">The upload value.</param>
public sealed record CreateFileCommand(FileUpload? Upload) : ICommand<Result<StoredFileId>>;

/// <summary>
/// Executes the command to create a file: the upload is validated, turned into its variants by
/// <see cref="IImageProcessor"/> and only those are stored; the upload itself is discarded.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Repository used to persist and retrieve storage.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="validator">The validator value.</param>
/// <param name="processor">Service that encodes the stored variants of the upload.</param>
public sealed class CreateFileCommandHandler(
    IStoredFileRepository files,
    IUnitOfWork uow,
    IFileStorage storage,
    ICurrentUser currentUser,
    IClock clock,
    FileUploadValidator validator,
    IImageProcessor processor
) : ICommandHandler<CreateFileCommand, Result<StoredFileId>>
{
    /// <summary>
    /// Handles the request to create a file.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<StoredFileId>> HandleAsync(
        CreateFileCommand command,
        CancellationToken ct = default
    )
    {
        var upload = command.Upload;

        var detection = await validator.ValidateAndDetectAsync(upload, ct);
        if (detection.IsFailure)
        {
            return detection.Error!;
        }

        var processed = await processor.ProcessAsync(upload!.Content, ct);
        if (processed.IsFailure)
        {
            return processed.Error!;
        }

        var file = StoredFile.Upload(
            FileNaming.SanitizeName(upload.FileName, AppStrings.FilesFallbackFileName),
            ProcessedImage.Format.Extension,
            currentUser.RequiredId(),
            clock.UtcNow
        );

        var saved = new List<string>();
        try
        {
            foreach (var variant in Enum.GetValues<FileVariant>())
            {
                var storedName = FileNaming.StoredName(file.Id, file.Extension, variant);
                await using var content = new MemoryStream(
                    processed.Value.Of(variant),
                    writable: false
                );
                await storage.SaveAsync(storedName, content, ct);
                saved.Add(storedName);
            }

            await files.AddAsync(file, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            saved.ForEach(storage.Delete);
            throw;
        }

        return file.Id;
    }
}
