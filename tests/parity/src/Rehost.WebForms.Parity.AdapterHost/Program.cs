using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Hosting;
using Rehost.WebForms.Parity.AdapterRunner;
using Rehost.WebForms.Parity.Contracts;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static async Task<int> Main(string[] args)
    {
        using var diagnostics = new RuntimeDiagnosticListener();

        try
        {
            var command = CommandLine.Parse(args);
            var basePath = AppContext.BaseDirectory;
            var manifestPath = command.ManifestPath
                ?? Path.Combine(basePath, "metadata", "sessions.json");
            var fixtureRoot = command.FixtureRoot ?? Path.Combine(basePath, "fixture");
            var manifest = InPhase("manifest", () => LoadManifest(manifestPath));

            if (command.Operation == Operation.RunSession)
            {
                var session = manifest.Sessions.FirstOrDefault(
                        candidate => string.Equals(
                            candidate.Name,
                            command.SessionName,
                            StringComparison.Ordinal))
                    ?? throw new InvalidOperationException(
                        "Manifest declares no session named '" + command.SessionName + "'.");

                var sessionObservation = await RunSessionAsync(session, fixtureRoot);
                Console.Out.WriteLine(JsonSerializer.Serialize(sessionObservation, JsonOptions));
                return 0;
            }

            var trace = new PipelineTrace
            {
                SchemaVersion = SchemaVersion,
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
                    .Select(session => InPhase(
                        "session:" + session.Name,
                        () => RunSessionProcess(session, manifestPath, fixtureRoot)))
                    .ToList()
            };

            if (command.Operation == Operation.Verify)
            {
                InPhase(
                    "verification",
                    () => Verify(
                        command.ExpectedPath
                            ?? Path.Combine(basePath, "oracle", "sessions.json"),
                        command.NormalizationPath
                            ?? Path.Combine(basePath, "metadata", "normalization.json"),
                        trace));
            }
            else
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(trace, JsonOptions));
            }

            return 0;
        }
        catch (PhaseException exception)
        {
            WriteDiagnostic(exception.Phase, exception.InnerException!);
            return 1;
        }
        catch (Exception exception)
        {
            WriteDiagnostic("command-line", exception);
            return 1;
        }
    }

    private static SessionManifest LoadManifest(string path)
    {
        path = Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Session manifest is absent.", path);
        }

        var manifest = JsonSerializer.Deserialize<SessionManifest>(
            File.ReadAllText(path),
            JsonOptions)
            ?? throw new InvalidDataException("Session manifest deserialized to null.");

        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported session manifest schema.");
        }

        if (manifest.Sessions.Count == 0)
        {
            throw new InvalidDataException("Session manifest declares no sessions.");
        }

        return manifest;
    }

    // A session observes a cold application, so it needs a process whose HttpRuntime singleton
    // and activated application have never been touched.
    private static SessionObservation RunSessionProcess(
        SessionSpecification session,
        string manifestPath,
        string fixtureRoot)
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The host process path is unavailable.");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // A dotted assembly name defeats GetFileNameWithoutExtension, which would read
        // "Rehost.WebForms.Parity.AdapterHost" as "AdapterParity"; compare against the apphost path instead.
        var entryAssembly = typeof(Program).Assembly.Location;
        var apphostPath = Path.ChangeExtension(
            entryAssembly,
            OperatingSystem.IsWindows() ? ".exe" : null);
        if (!string.Equals(executablePath, apphostPath, StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(entryAssembly);
        }

        startInfo.ArgumentList.Add("run-session");
        startInfo.ArgumentList.Add("--session");
        startInfo.ArgumentList.Add(session.Name);
        startInfo.ArgumentList.Add("--manifest");
        startInfo.ArgumentList.Add(manifestPath);
        startInfo.ArgumentList.Add("--fixtures");
        startInfo.ArgumentList.Add(fixtureRoot);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The session process failed to start.");
        // Both streams must drain concurrently. An observation larger than the pipe buffer,
        // which is 4 KB on Windows against 64 KB elsewhere, blocks the child mid-write while a
        // sequential reader waits on the stream it is not draining.
        var outputReader = process.StandardOutput.ReadToEndAsync();
        var errorReader = process.StandardError.ReadToEndAsync();
        Task.WaitAll(outputReader, errorReader);
        process.WaitForExit();

        if (errorReader.Result.Length > 0)
        {
            Console.Error.Write(errorReader.Result);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Session '" + session.Name + "' exited with code " + process.ExitCode + ".");
        }

        return JsonSerializer.Deserialize<SessionObservation>(outputReader.Result, JsonOptions)
            ?? throw new InvalidDataException(
                "Session '" + session.Name + "' produced no observation.");
    }

    private static async Task<SessionObservation> RunSessionAsync(
        SessionSpecification session,
        string fixtureRoot)
    {
        var basePath = AppContext.BaseDirectory;
        var applicationPath = Path.GetFullPath(
            Path.Combine(fixtureRoot, session.Fixture));
        var applicationId = "adapter-parity:" + session.Name;

        InPhase("fixture-validation", () => ValidateFixture(applicationPath));

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        InPhase(
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
            var drain = InPhase(
                "event-drain",
                () => CreateDrain(manager, applicationId, applicationPath));

            InPhase(
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
            EnsureTrailingDirectorySeparator(applicationPath),
            true,
            true);

        return registered as IPipelineEventDrain
            ?? throw new InvalidOperationException(
                "ApplicationManager did not return an IPipelineEventDrain.");
    }

    private static void ValidateFixture(string applicationPath)
    {
        applicationPath = Path.GetFullPath(applicationPath);
        var probePath = Path.Combine(applicationPath, "bin", "Rehost.WebForms.Parity.Probes.dll");
        var hostProbePath = Path.Combine(AppContext.BaseDirectory, "Rehost.WebForms.Parity.Probes.dll");

        if (!File.Exists(Path.Combine(applicationPath, "web.config")))
        {
            throw new FileNotFoundException(
                "Fixture web.config is missing.",
                Path.Combine(applicationPath, "web.config"));
        }

        if (!File.Exists(probePath))
        {
            throw new FileNotFoundException(
                "Probe handler/module assembly must exist only in fixture app/bin.",
                probePath);
        }

        if (File.Exists(hostProbePath))
        {
            throw new InvalidOperationException(
                "Probe assembly must not exist beside the host executable.");
        }

        if (typeof(Program).Assembly.GetReferencedAssemblies().Any(
                name => string.Equals(
                    name.Name,
                    "Rehost.WebForms.Parity.Probes",
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Host assembly must not reference Rehost.WebForms.Parity.Probes.");
        }

        if (AppDomain.CurrentDomain.GetAssemblies().Any(
                assembly => string.Equals(
                    assembly.GetName().Name,
                    "Rehost.WebForms.Parity.Probes",
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Probe assembly was loaded before application activation.");
        }
    }

    private static void Verify(
        string expectedPath,
        string normalizationPath,
        PipelineTrace actual)
    {
        expectedPath = Path.GetFullPath(expectedPath);
        normalizationPath = Path.GetFullPath(normalizationPath);

        ValidateEmptyNormalizationManifest(normalizationPath);

        if (!File.Exists(expectedPath))
        {
            throw new FileNotFoundException("Framework golden trace is absent.", expectedPath);
        }

        var expected = JsonSerializer.Deserialize<PipelineTrace>(
            File.ReadAllText(expectedPath),
            JsonOptions)
            ?? throw new InvalidDataException("Framework golden trace deserialized to null.");

        if (expected.SchemaVersion != actual.SchemaVersion)
        {
            throw new InvalidOperationException(
                "Parity mismatch at $.SchemaVersion: expected "
                + expected.SchemaVersion
                + ", actual "
                + actual.SchemaVersion
                + ".");
        }

        AdapterObservationComparer.VerifySessions(expected.Sessions, actual.Sessions);
        Console.Error.WriteLine(
            "Adapter observation matches the Framework golden trace across "
            + actual.Sessions.Count
            + " session(s).");
    }

    private static void ValidateEmptyNormalizationManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Normalization manifest is absent.", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported normalization manifest schema.");
        }

        var rules = root.GetProperty("rules");
        if (rules.ValueKind != JsonValueKind.Array || rules.GetArrayLength() != 0)
        {
            throw new InvalidDataException(
                "The parity normalization manifest must remain empty.");
        }
    }

    private static void InPhase(string phase, Action action)
    {
        try
        {
            action();
        }
        catch (PhaseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PhaseException(phase, exception);
        }
    }

    private static T InPhase<T>(string phase, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (PhaseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PhaseException(phase, exception);
        }
    }

    private static void WriteDiagnostic(string phase, Exception exception)
    {
        var diagnostic = new FailureDiagnostic
        {
            SchemaVersion = 1,
            Phase = phase,
            Exception = ExceptionObservation.FromException(exception)
        };
        Console.Error.WriteLine(JsonSerializer.Serialize(diagnostic, JsonOptions));
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private sealed class RuntimeDiagnosticListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Rehost.WebForms.Runtime")
            {
                EnableEvents(eventSource, EventLevel.Error);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name != "Rehost.WebForms.Runtime")
            {
                return;
            }

            Console.Error.WriteLine(
                "runtime/"
                + eventData.EventName
                + ": "
                + string.Join(
                    " | ",
                    eventData.Payload ?? (IEnumerable<object?>)Array.Empty<object?>()));
        }
    }

    private sealed class FailureDiagnostic
    {
        public int SchemaVersion { get; set; }

        public string Phase { get; set; } = "";

        public ExceptionObservation Exception { get; set; } = new();
    }

    private sealed class PhaseException : Exception
    {
        internal PhaseException(string phase, Exception innerException)
            : base("Adapter parity phase failed: " + phase + ".", innerException)
        {
            Phase = phase;
        }

        internal string Phase { get; }
    }
}
