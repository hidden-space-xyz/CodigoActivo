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
/// <param name="Upload">
/// Image uploaded, or <see langword="null"/> when the request carried none.
/// </param>
public sealed record CreateFileCommand(FileUpload? Upload) : ICommand<Result<StoredFileId>>;

/// <summary>
/// Executes the command to create a file: the upload is validated, turned into its variants by
/// <see cref="IImageProcessor"/> and only those are stored; the upload itself is discarded. When
/// the row cannot be saved, the stored variants are discarded before the failure is rethrown. A
/// file no content references is purged after <see cref="StoredFile.UnusedRetentionDays"/> days.
/// </summary>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="storage">Storage holding the content of the files.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the uploader.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="validator">Validator of the size and format of the upload.</param>
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
    /// <returns>
    /// A task whose result contains the identifier of the new file, or the <c>FileUpload*</c> error
    /// of the validation or of the image processing that refused it.
    /// </returns>
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

        await storage.SaveVariantsAsync(file.Id, processed.Value, ct);
        try
        {
            await files.AddAsync(file, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            storage.DiscardVariants(file.Id, file.Extension);
            throw;
        }

        return file.Id;
    }
}
