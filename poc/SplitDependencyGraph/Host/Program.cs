using SplitSample.Host;

var builder = WebApplication.CreateBuilder(args);
var host = builder.Build();
host.MapGet("/", Probe.Read);

if (args.Contains("--check"))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(Probe.Read()));
    return;
}

host.Run();
