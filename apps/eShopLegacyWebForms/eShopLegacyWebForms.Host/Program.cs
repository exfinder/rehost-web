using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rehost.WebForms.Hosting;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:5083";
var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));

// The content root defaults to the working directory, wherever the process was started.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseUrls(url);
builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "eshop-legacy-webforms";
    options.PhysicalRootPath = physicalRoot;
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWebForms();
app.Run();
