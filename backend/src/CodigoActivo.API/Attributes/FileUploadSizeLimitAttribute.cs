using CodigoActivo.Application.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Raises the request body limit of the annotated upload action, for both the server and the form
/// reader, to the maximum file size of <see cref="FileUploadOptions"/> plus an allowance for the
/// multipart overhead.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FileUploadSizeLimitAttribute : Attribute, IFilterFactory
{
    /// <summary>
    /// Gets whether the created filter may be reused across requests; it always may.
    /// </summary>
    public bool IsReusable => true;

    /// <summary>
    /// Creates the filter that applies the upload size limit.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve the filter dependencies.</param>
    /// <returns>The filter that sets the body size limits of the request.</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return new FileUploadSizeLimitFilter(
            serviceProvider.GetRequiredService<FileUploadOptions>()
        );
    }
}

internal sealed class FileUploadSizeLimitFilter(FileUploadOptions options) : IAuthorizationFilter
{
    internal const long MultipartOverheadBytes = 64 * 1024;

    /// <summary>
    /// Sets the body size limits of the request before the model binder reads the form; it never
    /// refuses the request itself.
    /// </summary>
    /// <param name="context">Authorization context of the request.</param>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var limitBytes = options.MaxSizeBytes + MultipartOverheadBytes;
        var features = context.HttpContext.Features;

        var bodySizeFeature = features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = limitBytes;
        }

        if (features.Get<IFormFeature>()?.Form is null)
        {
            features.Set<IFormFeature>(
                new FormFeature(
                    context.HttpContext.Request,
                    new FormOptions { MultipartBodyLengthLimit = limitBytes }
                )
            );
        }
    }
}
