using System.Net;
using System.Text.Json.Serialization;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.API.OpenApi;
using CodigoActivo.API.Security;
using CodigoActivo.Composition;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using IPNetwork = System.Net.IPNetwork;

namespace CodigoActivo.API.Configuration;

internal static class ApiServiceConfiguration
{
    private static readonly IPNetwork[] TrustedProxyNetworks =
    [
        new(IPAddress.Parse("127.0.0.0"), 8),
        new(IPAddress.Parse("::1"), 128),
        new(IPAddress.Parse("10.0.0.0"), 8),
        new(IPAddress.Parse("172.16.0.0"), 12),
        new(IPAddress.Parse("192.168.0.0"), 16),
        new(IPAddress.Parse("fc00::"), 7),
    ];

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

            foreach (var network in TrustedProxyNetworks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
    }

    private static void AddControllers(IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.AllowDuplicateProperties = false;
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)
                );
            })
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
