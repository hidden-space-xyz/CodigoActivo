using CodigoActivo.API.Configuration;
using CodigoActivo.API.Startup;

var builder = WebApplication.CreateBuilder(args);
using var logging = new ApiLogging();
WebApplication? app = null;

try
{
    builder.ConfigureApiHost();
    builder.AddApiServices();

    app = builder.Build();

    await app.InitializeAsync(app.Lifetime.ApplicationStopping);
    app.ConfigureRequestPipeline();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    logging.RecordFatal(ex, app?.Services);
    throw;
}
finally
{
    if (app is not null)
    {
        await app.DisposeAsync();
    }
}
