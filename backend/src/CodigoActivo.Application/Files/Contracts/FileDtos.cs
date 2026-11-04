namespace CodigoActivo.Application.Files.Contracts;

/// <summary>
/// Contains the file data returned by the API.
/// </summary>
/// <param name="Id">Identifier of the file.</param>
/// <param name="Extension">Extension of the stored format, without the dot.</param>
/// <param name="UploadedAt">UTC timestamp of the upload.</param>
public record FileResponse(Guid Id, string Extension, DateTimeOffset UploadedAt);
