using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Configuration;

internal static class ApiRequestPipeline
{
    internal static void ConfigureRequestPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodigoActivo API v1")
            );
        }

        app.UseHttpsRedirection();
        app.UseMiddleware<CacheControlMiddleware>();
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseMiddleware<CsrfValidationMiddleware>();
        app.UseOutputCache();
        app.MapControllers();
        app.MapFallback(
                "/api/{**path}",
                context => context.WriteApiErrorAsync(Error.NotFound(ErrorCode.EndpointNotFound))
            )
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
