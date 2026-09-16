namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the file data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Name">The name value.</param>
/// <param name="Extension">The extension value.</param>
/// <param name="UploadedAt">The uploaded at value.</param>
/// <param name="UploadedBy">The uploaded by value.</param>
public record FileResponse(
    Guid Id,
    string Name,
    string Extension,
    DateTimeOffset UploadedAt,
    Guid UploadedBy
);
