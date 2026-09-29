using Rehost.Web.AspNetCore;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.AddRehostWeb(options =>
{
    options.ApplicationId = "MyApp";
    options.PhysicalRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWeb();
app.Run();
