using Rehost.WebForms.Hosting;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "MyApp";
    options.PhysicalRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWebForms();
app.Run();
