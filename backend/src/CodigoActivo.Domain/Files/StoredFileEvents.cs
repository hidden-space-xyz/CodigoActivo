using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// A file was deleted.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record StoredFileDeleted(StoredFileId FileId) : IDomainEvent;
