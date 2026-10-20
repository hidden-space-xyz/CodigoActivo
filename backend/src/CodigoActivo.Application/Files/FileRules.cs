using System.Globalization;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;

namespace CodigoActivo.Application.Files;

/// <summary>
/// Creates safe, unique names for files stored by the application.
/// </summary>
public static class FileNaming
{
    private const int MaxNameLength = 260;

    /// <summary>
    /// Creates the unique storage name of one variant of a file, keeping its safe extension.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="extension">The extension value.</param>
    /// <param name="variant">Variant of the file the name stores.</param>
    /// <returns>The generated text.</returns>
    public static string StoredName(StoredFileId id, string extension, FileVariant variant)
    {
        return variant is FileVariant.Thumbnail
            ? $"{id}.thumbnail.{extension}"
            : $"{id}.{extension}";
    }

    /// <summary>
    /// Creates the storage names of every variant of a file.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="extension">The extension value.</param>
    /// <returns>The storage names, one per variant.</returns>
    public static IReadOnlyList<string> StoredNames(StoredFileId id, string extension)
    {
        return
        [
            .. Enum.GetValues<FileVariant>().Select(variant => StoredName(id, extension, variant)),
        ];
    }

    /// <summary>
    /// Reads the identifier of the file a storage name belongs to.
    /// </summary>
    /// <param name="storedName">Storage name to read.</param>
    /// <param name="id">Identifier of the file, when the name starts with one.</param>
    /// <returns><see langword="true"/> when the name starts with a file identifier and a dot.</returns>
    public static bool TryReadId(string storedName, out StoredFileId id)
    {
        ArgumentNullException.ThrowIfNull(storedName);
        var dot = storedName.IndexOf('.', StringComparison.Ordinal);
        if (dot > 0 && Guid.TryParseExact(storedName.AsSpan(0, dot), "D", out var value))
        {
            id = StoredFileId.From(value);
            return true;
        }

        id = default;
        return false;
    }

    /// <summary>
    /// Keeps only the last segment of a client-supplied name, without control or invisible
    /// formatting characters (a right-to-left override can disguise the extension), trimmed and cut
    /// to 260 characters.
    /// </summary>
    /// <param name="fileName">The file name value.</param>
    /// <param name="fallbackName">Name used when nothing remains.</param>
    /// <returns>The generated text.</returns>
    public static string SanitizeName(string? fileName, string fallbackName)
    {
        var normalizedFileName = (fileName ?? string.Empty).Replace('\\', '/');
        var name = string.Concat(Path.GetFileName(normalizedFileName).Where(IsVisible)).Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = fallbackName;
        }

        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }

    private static bool IsVisible(char character)
    {
        return !char.IsControl(character)
            && char.GetUnicodeCategory(character) != UnicodeCategory.Format;
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
            return Error.Validation(ApplicationErrorCode.FileUploadMissing);
        }

        if (upload.Length <= 0)
        {
            return Error.Validation(ApplicationErrorCode.FileUploadEmpty);
        }

        if (upload.Length > options.MaxSizeBytes)
        {
            return Error.Validation(ApplicationErrorCode.FileUploadTooLarge);
        }

        if (!upload.Content.CanSeek)
        {
            return Error.Validation(ApplicationErrorCode.FileUploadStreamNotSeekable);
        }

        upload.Content.Position = 0;
        var format = await upload.Content.DetectImageFormatAsync(ct);
        if (format is null)
        {
            return Error.Validation(ApplicationErrorCode.FileUploadUnsupportedFormat);
        }

        upload.Content.Position = 0;
        return format;
    }
}
