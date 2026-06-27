using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CodigoActivo.API.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class InvalidatesCacheAttribute(params string[] groups)
    : Attribute,
        IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var executed = await next();

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        if (IsSuccess(executed.Result))
        {
            context
                .HttpContext.RequestServices.GetRequiredService<IResponseCacheService>()
                .InvalidateGroups(groups);
        }
    }

    private static bool IsSuccess(IActionResult? result)
    {
        return result switch
        {
            IStatusCodeActionResult { StatusCode: { } status } => status is >= 200 and < 300,
            null => false,
            _ => true,
        };
    }
}
