using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rehost.WebForms.Hosting;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:5081";
var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls(url);
builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "webforms-application";
    options.PhysicalRootPath = physicalRoot;
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWebForms();
app.Run();
