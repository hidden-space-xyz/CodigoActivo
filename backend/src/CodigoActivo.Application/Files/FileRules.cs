using CodigoActivo.Application.Extensions;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Files;

public static class FileNaming
{
    private const int MaxNameLength = 260;

    public static string StoredName(Guid id, string extension)
    {
        return $"{id}.{extension}";
    }

    public static string SanitizeName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = AppStrings.FilesFallbackFileName;
        }

        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }
}

public sealed class FileUploadValidator(FileUploadOptions options)
{
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
