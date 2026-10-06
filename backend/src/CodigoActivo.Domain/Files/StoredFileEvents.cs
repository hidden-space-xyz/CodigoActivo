using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// The content of a file was replaced.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record StoredFileReplaced(StoredFileId FileId) : IDomainEvent;

/// <summary>
/// A file was deleted.
/// </summary>
/// <param name="FileId">Identifier of the file.</param>
public sealed record StoredFileDeleted(StoredFileId FileId) : IDomainEvent;
