using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Rehost.WebForms.Hosting;
using WingtipToys.Host;

var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));

// The content root defaults to the working directory, wherever the process was started.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddHostedService<PayPalNvpResponder>();
builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "wingtip-toys";
    options.PhysicalRootPath = physicalRoot;
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWebForms();
app.Run();
