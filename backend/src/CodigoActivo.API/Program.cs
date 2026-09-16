using CodigoActivo.API.Configuration;
using CodigoActivo.API.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureApiHost();
builder.AddApiServices();

await using var app = builder.Build();

await app.InitializeAsync(app.Lifetime.ApplicationStopping);
app.ConfigureRequestPipeline();

await app.RunAsync();
