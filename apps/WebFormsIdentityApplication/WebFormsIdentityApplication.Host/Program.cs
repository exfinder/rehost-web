using System.Data.Common;
using System.Data.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rehost.WebForms.Hosting;

var url = args.Length > 0 ? args[0] : "http://127.0.0.1:5082";
var physicalRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
// Provider services come from <entityFramework> in web.config, but the factory behind them
// cannot: .NET dropped the <system.data> registry the Framework read. SQLiteFactory, not the
// EF6 provider factory, because EF reverse-maps the connection's own factory type to a name.
DbProviderFactories.RegisterFactory("System.Data.SQLite.EF6", SQLiteFactory.Instance);

// Raw DDL rather than Entity Framework: reaching EF before the application initializes
// installs the configuration system, and HostingEnvironment then refuses to start.
var dataDirectory = Path.Combine(physicalRoot, "App_Data");
Directory.CreateDirectory(dataDirectory);
var identityDatabase = Path.Combine(dataDirectory, "Identity.db");
if (!File.Exists(identityDatabase))
{
    using var connection = new SQLiteConnection("Data Source=" + identityDatabase);
    connection.Open();
    using var schema = connection.CreateCommand();
    schema.CommandText = File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Identity.schema.sql"));
    schema.ExecuteNonQuery();
}

// The content root defaults to the working directory, wherever the process was started.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseUrls(url);
builder.AddRehostWebForms(options =>
{
    options.ApplicationId = "webforms-identity-application";
    options.PhysicalRootPath = physicalRoot;
    options.VirtualRootPath = "/";
});

var app = builder.Build();
app.UseRehostWebForms();
app.Run();
