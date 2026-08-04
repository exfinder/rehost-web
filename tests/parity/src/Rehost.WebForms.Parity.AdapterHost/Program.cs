using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Hosting;
using Rehost.WebForms.Parity.Contracts;
using Rehost.WebForms.Parity.Harness;
using Rehost.WebForms.Parity.Runner;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rehost.WebForms.Hosting;

namespace Rehost.WebForms.Parity.AdapterHost;

internal static class Program
{
    private const int SchemaVersion = 2;

    // The asynchronous handler parks until its gate opens. Releasing after the request is in
    // flight keeps the completion on a thread the pipeline has already let go of; overshooting
    // only makes the request more asynchronous.
    private static readonly TimeSpan GateReleaseDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);

    private static readonly ParityOperation[] SupportedOperations =
    {
        ParityOperation.Run,
        ParityOperation.Verify,
        ParityOperation.RunSession
    };

    public static async Task<int> Main(string[] args)
    {
        using var diagnostics = new RuntimeDiagnosticListener();

        try
        {
            var command = ParityCommandLine.Parse(
                args,
                SupportedOperations,
                "dotnet Rehost.WebForms.Parity.AdapterHost.dll");
            var basePath = AppContext.BaseDirectory;
            var manifestPath = command.ManifestPath
                ?? Path.Combine(basePath, "metadata", "sessions.json");
            var fixtureRoot = command.FixtureRoot ?? Path.Combine(basePath, "fixture");
            var manifest = PhaseRunner.InPhase(
                "manifest",
                () => ManifestLoader.Load(manifestPath));

            if (command.Operation == ParityOperation.RunSession)
            {
                var session = ManifestLoader.FindSession(manifest, command.SessionName!);
                var sessionObservation = await RunSessionAsync(session, fixtureRoot);
                Console.Out.WriteLine(ParityJson.Serialize(sessionObservation));
                return 0;
            }

            var trace = new PipelineTrace
            {
                SchemaVersion = SchemaVersion,
                // Frozen: compared against the committed golden.
                Provenance = new TraceProvenance
                {
                    Oracle = "Rehost WebForms ASP.NET Core adapter",
                    TargetFramework = "net10.0",
                    RuntimeRequirement = ".NET 10",
                    ManagedEntryPoint =
                        "System.Web.HttpRuntime.ProcessRequest(HttpWorkerRequest)",
                    ActivationEntryPoint =
                        "AddRehostWebForms + UseRehostWebForms over Kestrel",
                    Fixture = "precompiled-handler-and-request-body-v2"
                },
                Sessions = manifest.Sessions
                    .Select(session => PhaseRunner.InPhase(
                        "session:" + session.Name,
                        () => SessionChildProcess.Run(
                            typeof(Program).Assembly,
                            session,
                            manifestPath,
                            fixtureRoot)))
                    .ToList()
            };

            if (command.Operation == ParityOperation.Verify)
            {
                PhaseRunner.InPhase(
                    "verification",
                    () =>
                    {
                        GoldenTrace.Verify(
                            command.ExpectedPath
                                ?? Path.Combine(basePath, "oracle", "sessions.json"),
                            trace,
                            TraceComparison.Adapter);
                        Console.Error.WriteLine(
                            "Adapter observation matches the Framework golden trace across "
                            + trace.Sessions.Count
                            + " session(s).");
                    });
            }
            else
            {
                Console.Out.WriteLine(ParityJson.Serialize(trace));
            }

            return 0;
        }
        catch (PhaseException exception)
        {
            DiagnosticWriter.Write(exception.Phase, exception.InnerException!);
            return 1;
        }
        catch (Exception exception)
        {
            DiagnosticWriter.Write("command-line", exception);
            return 1;
        }
    }

    private static async Task<SessionObservation> RunSessionAsync(
        SessionSpecification session,
        string fixtureRoot)
    {
        var basePath = AppContext.BaseDirectory;
        var applicationPath = Path.GetFullPath(
            Path.Combine(fixtureRoot, session.Fixture));
        var applicationId = "adapter-parity:" + session.Name;

        PhaseRunner.InPhase(
            "fixture-validation",
            () => FixtureValidator.Validate(applicationPath, typeof(Program).Assembly));

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        PhaseRunner.InPhase(
            "host-registration",
            () => builder.AddRehostWebForms(options =>
            {
                options.ApplicationId = applicationId;
                options.PhysicalRootPath = applicationPath;
                options.VirtualRootPath = "/";
                options.MachineConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    "rehost-webforms.machine.config");
                options.RootWebConfigurationFilePath = Path.Combine(
                    basePath,
                    "configs",
                    "rehost-webforms.web.config");
            }));

        var app = builder.Build();
        app.UseRehostWebForms();

        await app.StartAsync();

        try
        {
            var address = app.Urls.FirstOrDefault()
                ?? throw new InvalidOperationException("Kestrel reported no bound address.");

            using var client = CreateClient(address);
            var observation = new SessionObservation { Name = session.Name };

            foreach (var step in session.Steps)
            {
                observation.Requests.AddRange(await RunStepAsync(client, step));
            }

            var manager = ApplicationManager.GetApplicationManager();
            var drain = PhaseRunner.InPhase(
                "event-drain",
                () => CreateDrain(manager, applicationId, applicationPath));

            PhaseRunner.InPhase(
                "event-drain",
                () => manager.StopObject(applicationId, typeof(AdapterSessionRunner)));
            observation.ApplicationEvents = drain.DrainApplicationEvents();
            observation.SessionEvents = drain.DrainSessionEvents();

            return observation;
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<List<RequestObservation>> RunStepAsync(
        HttpClient client,
        List<RequestSpecification> step)
    {
        foreach (var request in step)
        {
            PipelineEventJournal.OpenRequest(request.Name);
        }

        ParityBarrier.Begin(step.Count);

        var pending = step.Select(request => ObserveAsync(client, request)).ToList();

        await Task.Delay(GateReleaseDelay);
        foreach (var request in step)
        {
            ParityGate.Open(request.Name);
        }

        return (await Task.WhenAll(pending)).ToList();
    }

    private static async Task<RequestObservation> ObserveAsync(
        HttpClient client,
        RequestSpecification request)
    {
        var target = string.IsNullOrEmpty(request.QueryString)
            ? request.Path
            : request.Path + "?" + request.QueryString;

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), target)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        message.Headers.TryAddWithoutValidation(
            PipelineEventJournal.RequestHeaderName,
            request.Name);

        if (!string.IsNullOrEmpty(request.BodyFraming))
        {
            var requestBody = Convert.FromBase64String(request.BodyBase64);
            message.Content = string.Equals(
                request.BodyFraming,
                "chunked",
                StringComparison.Ordinal)
                ? new ChunkedContent(requestBody)
                : new ByteArrayContent(requestBody);
            message.Content.Headers.ContentType = new("application/octet-stream");
        }

        using var response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseContentRead);
        var body = await response.Content.ReadAsByteArrayAsync();

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .SelectMany(header => header.Value.Select(
                value => new HeaderObservation { Name = header.Key, Value = value }))
            .ToList();

        return new RequestObservation
        {
            Name = request.Name,
            Observation = new PipelineObservation
            {
                Events = PipelineEventJournal.DrainRequest(request.Name),
                Response = new ResponseObservation
                {
                    StatusCode = (int)response.StatusCode,
                    StatusDescription = response.ReasonPhrase ?? "",
                    Headers = headers,
                    BodyBase64 = Convert.ToBase64String(body)
                }
            }
        };
    }

    private static HttpClient CreateClient(string address)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
            AllowAutoRedirect = false
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(address),
            Timeout = ClientTimeout,
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        client.DefaultRequestHeaders.Clear();
        return client;
    }

    private static IPipelineEventDrain CreateDrain(
        ApplicationManager manager,
        string applicationId,
        string applicationPath)
    {
        // Registering a second object cannot skew applications-created: that counts
        // HttpApplication instances, and a registered object is not one.
        var registered = manager.CreateObject(
            applicationId,
            typeof(AdapterSessionRunner),
            "/",
            PathUtilities.EnsureTrailingDirectorySeparator(applicationPath),
            true,
            true);

        return registered as IPipelineEventDrain
            ?? throw new InvalidOperationException(
                "ApplicationManager did not return an IPipelineEventDrain.");
    }

    private sealed class ChunkedContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            return stream.WriteAsync(body).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
