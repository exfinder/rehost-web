using Rehost.WebForms.Hosting;
using Serilog;

var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));

// The working directory is the served site root; appsettings.json must stay under bin.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "yetanotherforum";
    options.PhysicalRootPath = physicalRoot;
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseRehostWebForms();
app.Run();
