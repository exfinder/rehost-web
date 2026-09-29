using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rehost.Web.AspNetCore;


var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.AddRehostWeb(options =>
{
    options.ApplicationId = "sample";
    options.PhysicalRootPath = FindApplicationRoot();
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWeb();

app.Run();

// rehost_root/ lays out like a classic Web Site — pages, App_Code, and web.config at its
// root, binaries under bin/ — so the root is the nearest ancestor of the binary carrying the
// web.config, and pages are compiled from source: an edit needs a restart, not a rebuild.
static string FindApplicationRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
        directory != null;
        directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "web.config")))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException(
        "No web.config found between " + AppContext.BaseDirectory + " and the filesystem root.");
}
