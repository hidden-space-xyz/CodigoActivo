using CodigoActivo.API.Configuration;
using CodigoActivo.API.Diagnostics;
using CodigoActivo.API.Startup;

var builder = WebApplication.CreateBuilder(args);
using var logging = ApiLogging.Start(builder.Configuration);

try
{
    builder.ConfigureApiHost(logging);
    builder.AddApiServices();

    await using var app = builder.Build();

    await app.InitializeAsync(app.Lifetime.ApplicationStopping);
    app.ConfigureRequestPipeline();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logging.CreateLogger(LogCategories.Lifecycle).HostTerminatedUnexpectedly(ex);
    throw;
}
