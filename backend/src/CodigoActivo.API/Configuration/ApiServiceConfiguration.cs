using System.Text.Json.Serialization;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.API.OpenApi;
using CodigoActivo.API.Security;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Configuration;

internal static class ApiServiceConfiguration
{
    internal static void AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddCodigoActivo(builder.Configuration);
        builder.Services.AddScoped<SessionTicketValidator>();
        builder.Services.AddScoped<TwoFactorTicketValidator>();
        builder.Services.AddSingleton<DeploymentModeLock>();

        AddForwardedHeaders(builder.Services);
        AddControllers(builder.Services);
        builder.AddApiSecurity();
        builder.Services.AddApiOutputCaching();
        builder.Services.AddApiRateLimiting();
        AddErrorHandling(builder.Services);
        AddOpenApi(builder.Services);
    }

    private static void AddForwardedHeaders(IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    private static void AddControllers(IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
            )
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var (statusCode, body) = ApiErrorResponseExtensions.Create(
                        Error.BadRequest(ErrorCode.RequestValidationFailed),
                        context.HttpContext
                    );
                    return new ObjectResult(body) { StatusCode = statusCode };
                };
            });
    }

    private static void AddErrorHandling(IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
    }

    private static void AddOpenApi(IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<JsonResponseMediaTypeFilter>();
            options.OperationFilter<CamelCaseQueryParametersFilter>();
            options.DocumentFilter<ApiErrorResponseDocumentFilter>();
        });
    }
}
