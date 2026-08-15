using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rehost.WebForms.Hosting;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:5082";
var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
var codegen = Path.Combine(AppContext.BaseDirectory, "codegen");
Directory.CreateDirectory(codegen);

var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls(url);
builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "webforms-identity-application";
    options.PhysicalRootPath = physicalRoot;
    options.VirtualRootPath = "/";
    options.CompilationTempDirectory = codegen;
    options.MachineConfigurationFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "configs",
        "rehost-webforms.machine.config");
    options.RootWebConfigurationFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "configs",
        "rehost-webforms.web.config");
});

var app = builder.Build();
app.UseRehostWebForms();
app.Run();
