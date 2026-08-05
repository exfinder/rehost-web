using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rehost.WebForms.Hosting;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:5080";

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls(url);

var codegen = Path.Combine(AppContext.BaseDirectory, "codegen");
Directory.CreateDirectory(codegen);

builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "sample";
    options.PhysicalRootPath = Path.Combine(AppContext.BaseDirectory, "webroot");
    options.VirtualRootPath = "/";
    options.CompilationTempDirectory = codegen;
    options.MachineConfigurationFilePath = Path.Combine(
        AppContext.BaseDirectory, "configs", "rehost-webforms.machine.config");
    options.RootWebConfigurationFilePath = Path.Combine(
        AppContext.BaseDirectory, "configs", "rehost-webforms.web.config");
});

var app = builder.Build();
app.UseRehostWebForms();

Console.WriteLine($"Sample Web Forms application: {url}/Default.aspx");
app.Run();
