using CodigoActivo.Application.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class FileUploadSizeLimitAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => true;

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
