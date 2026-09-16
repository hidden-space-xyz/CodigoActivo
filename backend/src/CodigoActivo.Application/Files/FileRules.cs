using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Creates safe, unique names for files stored by the application.
/// </summary>
public static class FileNaming
{
    private const int MaxNameLength = 260;

    /// <summary>
    /// Creates a unique storage name while preserving the safe file extension.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="extension">The extension value.</param>
    /// <returns>The generated text.</returns>
    public static string StoredName(Guid id, string extension)
    {
        return $"{id}.{extension}";
    }

    /// <summary>
    /// Removes unsafe characters from the name.
    /// </summary>
    /// <param name="fileName">The file name value.</param>
    /// <returns>The generated text.</returns>
    public static string SanitizeName(string? fileName)
    {
        var normalizedFileName = (fileName ?? string.Empty).Replace('\\', '/');
        var name = Path.GetFileName(normalizedFileName).Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = AppStrings.FilesFallbackFileName;
        }

        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }
}

/// <summary>
/// Validates file upload input before it is processed.
/// </summary>
/// <param name="options">Configuration values used by the component.</param>
public sealed class FileUploadValidator(FileUploadOptions options)
{
    /// <summary>
    /// Validates the and detect and returns the normalized result.
    /// </summary>
    /// <param name="upload">The upload value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an image format on success, or an application error on failure.</returns>
    public async Task<Result<ImageFormat>> ValidateAndDetectAsync(
        FileUpload? upload,
        CancellationToken ct = default
    )
    {
        if (upload is null)
        {
            return Error.BadRequest(ErrorCode.FileUploadMissing);
        }

        if (upload.Length <= 0)
        {
            return Error.BadRequest(ErrorCode.FileUploadEmpty);
        }

        if (upload.Length > options.MaxSizeBytes)
        {
            return Error.BadRequest(ErrorCode.FileUploadTooLarge);
        }

        if (!upload.Content.CanSeek)
        {
            return Error.BadRequest(ErrorCode.FileUploadStreamNotSeekable);
        }

        upload.Content.Position = 0;
        var format = await upload.Content.DetectImageFormatAsync(ct);
        if (format is null)
        {
            return Error.BadRequest(ErrorCode.FileUploadUnsupportedFormat);
        }

        upload.Content.Position = 0;
        return format;
    }
}
