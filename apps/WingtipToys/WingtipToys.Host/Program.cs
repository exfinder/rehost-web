using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Rehost.WebForms.Hosting;
using WingtipToys.Host;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:5085";
var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));

// The content root defaults to the working directory, wherever the process was started.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseUrls(url);
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
