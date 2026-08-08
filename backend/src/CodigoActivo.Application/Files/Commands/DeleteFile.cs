using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;

namespace CodigoActivo.Application.Files.Commands;

public sealed record DeleteFileCommand(Guid FileId) : ICommand<Result>;

public sealed class DeleteFileCommandHandler(
    IFileRepository files,
    IUnitOfWork uow,
    ILocalFileSystemRepository storage,
    ICacheInvalidator cacheInvalidator
) : ICommandHandler<DeleteFileCommand, Result>
{
    public async Task<Result> HandleAsync(DeleteFileCommand command, CancellationToken ct = default)
    {
        var file = await files.FindAsync(f => f.Id == command.FileId, ct);
        if (file is null)
        {
            return Error.NotFound(ErrorCode.FileNotFound);
        }

        if (await files.IsInUseAsync(command.FileId, ct))
        {
            return Error.Conflict(ErrorCode.FileInUse);
        }

        var storedName = FileNaming.StoredName(file.Id, file.Extension);

        files.Remove(file);
        await uow.SaveChangesAsync(ct);

        storage.Delete(storedName);
        await cacheInvalidator.InvalidateAsync(CacheTags.Files);
        return Result.Success();
    }
}
