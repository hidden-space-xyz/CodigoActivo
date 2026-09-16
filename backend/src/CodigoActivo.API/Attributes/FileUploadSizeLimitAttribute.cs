using CodigoActivo.Application.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

/// <summary>
/// Applies file upload size limit validation or authorization to the annotated target.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FileUploadSizeLimitAttribute : Attribute, IFilterFactory
{
    /// <summary>
    /// Gets whether reusable.
    /// </summary>
    public bool IsReusable => true;

    /// <summary>
    /// Creates an instance.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve the filter dependencies.</param>
    /// <returns>The resulting filter metadata value.</returns>
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
    /// Authorizes the current request against the attribute requirements.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
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
