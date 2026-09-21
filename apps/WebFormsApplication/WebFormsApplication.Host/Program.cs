using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rehost.WebForms.Hosting;

var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));

// The content root defaults to the working directory, wherever the process was started.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "webforms-application";
    options.PhysicalRootPath = physicalRoot;
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWebForms();
app.Run();
