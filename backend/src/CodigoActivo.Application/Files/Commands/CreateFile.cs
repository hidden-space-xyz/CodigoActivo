using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Commands;

public sealed record CreateFileCommand(FileUpload? Upload, Guid UserId)
    : ICommand<Result<FileResponse>>;

public sealed class CreateFileCommandHandler(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    IClock clock,
    FileUploadValidator validator
) : ICommandHandler<CreateFileCommand, Result<FileResponse>>
{
    public async Task<Result<FileResponse>> HandleAsync(
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

        var format = detection.Value;
        var file = new FileEntity
        {
            Name = FileNaming.SanitizeName(upload!.FileName),
            Extension = format.Extension,
            UploadedAt = clock.UtcNow,
            UploadedBy = command.UserId,
        };

        var storedName = FileNaming.StoredName(file.Id, file.Extension);
        await storage.SaveAsync(storedName, upload.Content, ct);

        try
        {
            await files.AddAsync(file, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(storedName);
            throw;
        }

        return file.ToResponse();
    }
}
