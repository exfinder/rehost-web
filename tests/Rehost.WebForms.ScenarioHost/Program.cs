using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rehost.WebForms.Hosting;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.ScenarioHost;

// One application per process is a hard constraint of the runtime, so any test that activates an
// application needs its own process. This host runs one named scenario against one fixture and
// exits, which keeps that constraint in the process boundary instead of in test ordering rules.
//
// It deliberately does not compare against the .NET Framework oracle; that is the parity
// prototype's job. See ADR 0043.
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = ScenarioOptions.Parse(args);
            Run(options);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run(ScenarioOptions options)
    {
        Environment.SetEnvironmentVariable(TraceJournal.TraceVariable, options.TracePath);

        if (options.Serve)
        {
            ServeAsync(options).GetAwaiter().GetResult();
            return;
        }

        WebFormsApplication.Initialize(new WebFormsApplicationOptions
        {
            ApplicationId = options.ApplicationId,
            PhysicalRootPath = options.ApplicationPath,
            VirtualRootPath = "/",
            CompilationTempDirectory = options.CompilationTempDirectory,
            MachineConfigurationFilePath = options.MachineConfigurationPath ?? Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                "rehost-webforms.machine.config"),
            RootWebConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                "rehost-webforms.web.config"),
        });

        var manager = ApplicationManager.GetApplicationManager();
        manager.Open();

        var runner = (ScenarioRunner)manager.CreateObject(
            options.ApplicationId,
            typeof(ScenarioRunner),
            "/",
            EnsureTrailingSeparator(options.ApplicationPath),
            failIfExists: true,
            // Mirrors the production adapter: initialization failures surface per request.
            throwOnError: false);

        try
        {
            TraceJournal.Record("codegen-dir:" + HttpRuntime.CodegenDir);
            TraceJournal.Record("private-bytes-limit:" + HttpRuntime.Cache.EffectivePrivateBytesLimit);

            for (var i = 0; i < options.Requests.Count; i++)
            {
                var path = options.Requests[i];
                var responsePath = options.ResponseDirectory == null
                    ? null
                    : Path.Combine(options.ResponseDirectory, i + ".body");

                var status = runner.Request(path, responsePath);
                TraceJournal.Record("request:" + path + ":" + status);
            }

            // Holding the process alive keeps its generated assemblies loaded, which is the only
            // way a second process can meet a file it is not allowed to delete. The caller owns
            // the gate and releases it when it is done, so the hold lasts exactly as long as the
            // overlap being tested rather than a guessed interval.
            if (options.HoldGate != null)
            {
                HoldUntilReleased(options.HoldGate);
            }
        }
        finally
        {
            manager.StopObject(options.ApplicationId, typeof(ScenarioRunner));
            manager.ShutdownApplication(options.ApplicationId);
            manager.Close();
        }
    }

    // The same application, reached over a socket through the production adapter rather than by
    // calling HttpRuntime.ProcessRequest directly.
    private static async Task ServeAsync(ScenarioOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            if (options.BodyProbes.Any(probe => probe.EndsWith("kestrel-too-large", StringComparison.Ordinal)))
            {
                kestrel.Limits.MaxRequestBodySize = 1024;
            }
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.AddRehostWebForms(configured =>
        {
            configured.ApplicationId = options.ApplicationId;
            configured.PhysicalRootPath = options.ApplicationPath;
            configured.VirtualRootPath = "/";
            configured.CompilationTempDirectory = options.CompilationTempDirectory;
            configured.MachineConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                "rehost-webforms.machine.config");
            configured.RootWebConfigurationFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "configs",
                "rehost-webforms.web.config");
        });

        var app = builder.Build();
        app.UseRehostWebForms();

        await app.StartAsync();

        try
        {
            var address = app.Urls.FirstOrDefault()
                ?? throw new InvalidOperationException("Kestrel reported no bound address.");

            using var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 1,
                AllowAutoRedirect = false,
            };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
            client.Timeout = TimeSpan.FromSeconds(30);

            if (options.Postbacks.Count != 0)
            {
                var index = 0;
                foreach (var probe in options.Postbacks)
                {
                    index = await RunPostbackAsync(client, options, probe, index);
                }
            }
            else if (options.BodyProbes.Count != 0)
            {
                for (var i = 0; i < options.BodyProbes.Count; i++)
                {
                    await RunBodyProbeAsync(client, options, options.BodyProbes[i], i);
                }
            }
            else
            {
                for (var i = 0; i < options.Requests.Count; i++)
                {
                    var path = options.Requests[i];
                    using var response = await client.GetAsync(path);
                    await RecordResponseAsync(options, path, response, i);
                }
            }
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task<int> RunPostbackAsync(
        HttpClient client,
        ScenarioOptions options,
        string probe,
        int index)
    {
        if (probe == "cross-page")
        {
            return await RunCrossPagePostbackAsync(client, options, index);
        }

        if (probe.StartsWith("captured", StringComparison.Ordinal))
        {
            return await RunCapturedPostbackAsync(client, options, probe, index);
        }

        if (probe.StartsWith("upload", StringComparison.Ordinal))
        {
            return await RunUploadAsync(client, options, probe, index);
        }

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Message"] = "typed by the client",
        };
        var rounds = 1;
        var tamper = false;

        switch (probe)
        {
            case "apply":
                overrides["Apply"] = "Apply";
                break;
            case "tamper":
                overrides["Apply"] = "Apply";
                tamper = true;
                break;
            case "unsafe-input":
                overrides["Apply"] = "Apply";
                overrides["Message"] = "<script>alert(1)</script>";
                break;
            // State restored across a single postback is indistinguishable from state built
            // during it, so the control-state claim needs a second round.
            case "apply-twice":
                overrides["Apply"] = "Apply";
                rounds = 2;
                break;
            case "bump":
                overrides["__EVENTTARGET"] = "Bump";
                overrides["__EVENTARGUMENT"] = "";
                break;
            default:
                throw new ArgumentException("Unrecognized postback probe: " + probe);
        }

        string html;
        using (var rendered = await client.GetAsync("/Default.aspx"))
        {
            html = await rendered.Content.ReadAsStringAsync();
            await RecordResponseAsync(options, probe + ":render", rendered, index++);

            if (!rendered.IsSuccessStatusCode)
            {
                return index;
            }
        }

        for (var round = 0; round < rounds; round++)
        {
            if (tamper)
            {
                overrides["__VIEWSTATE"] =
                    FlipOneCharacter(PostbackFormClient.ReadFields(html)["__VIEWSTATE"]);
            }

            using var response = await PostFormAsync(
                client,
                PostbackFormClient.FormAction(html),
                PostbackFormClient.BuildBody(html, overrides));

            html = await response.Content.ReadAsStringAsync();
            await RecordResponseAsync(options, probe + ":postback", response, index++);

            if (!response.IsSuccessStatusCode)
            {
                break;
            }
        }

        return index;
    }

    // Their __VIEWSTATEGENERATOR values differ, which is what tells the page the payload was not
    // meant for it.
    private static async Task<int> RunCrossPagePostbackAsync(
        HttpClient client,
        ScenarioOptions options,
        int index)
    {
        string otherHtml;
        using (var other = await client.GetAsync("/Other.aspx"))
        {
            otherHtml = await other.Content.ReadAsStringAsync();
            await RecordResponseAsync(options, "cross-page:other", other, index++);
        }

        var borrowed = PostbackFormClient.ReadFields(otherHtml);

        string html;
        using (var rendered = await client.GetAsync("/Default.aspx"))
        {
            html = await rendered.Content.ReadAsStringAsync();
            await RecordResponseAsync(options, "cross-page:render", rendered, index++);
        }

        // Only the borrowed state travels. An event validation field is bound to the exact
        // __VIEWSTATE it was issued with, so carrying one would fail event validation before MAC
        // suppression could be observed, and posting no control values keeps ValidateEvent off
        // the path entirely.
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["__VIEWSTATE"] = borrowed["__VIEWSTATE"],
            ["__VIEWSTATEGENERATOR"] = borrowed["__VIEWSTATEGENERATOR"],
            ["__EVENTTARGET"] = "",
            ["__EVENTARGUMENT"] = "",
        };

        using var response = await PostFormAsync(
            client,
            PostbackFormClient.FormAction(html),
            PostbackFormClient.Encode(fields));
        await RecordResponseAsync(options, "cross-page:postback", response, index++);

        return index;
    }

    // Replays a postback another runtime rendered, which is what a load-balanced farm spanning
    // both does on every request that lands on the node that did not render the page. The payload
    // ships with the fixture because it is only valid for that page under that key.
    private static async Task<int> RunCapturedPostbackAsync(
        HttpClient client,
        ScenarioOptions options,
        string probe,
        int index)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in File.ReadAllLines(
            Path.Combine(options.ApplicationPath, "Framework.postback")))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var split = line.IndexOf('=');
            fields[line[..split]] = line[(split + 1)..];
        }

        if (probe == "captured-without-event-validation")
        {
            fields.Remove("__EVENTVALIDATION");
        }

        // The render is not the subject, but it proves the page this payload names still serves,
        // so a failure below cannot be blamed on the fixture being broken.
        string html;
        using (var rendered = await client.GetAsync("/Default.aspx"))
        {
            html = await rendered.Content.ReadAsStringAsync();
            await RecordResponseAsync(options, probe + ":render", rendered, index++);
        }

        using var response = await PostFormAsync(
            client,
            PostbackFormClient.FormAction(html),
            PostbackFormClient.Encode(fields));
        await RecordResponseAsync(options, probe + ":postback", response, index++);

        return index;
    }

    private static async Task<int> RunUploadAsync(
        HttpClient client,
        ScenarioOptions options,
        string probe,
        int index)
    {
        string html;
        using (var rendered = await client.GetAsync("/Upload.aspx"))
        {
            html = await rendered.Content.ReadAsStringAsync();
            await RecordResponseAsync(options, probe + ":render", rendered, index++);
        }

        var fields = PostbackFormClient.ReadFields(html);
        fields["Note"] = "a note";
        fields["Save"] = "Save";

        var files = new List<MultipartFile>();
        switch (probe)
        {
            case "upload":
                files.Add(new MultipartFile(
                    "Picked",
                    "notes.txt",
                    "text/plain",
                    Encoding.UTF8.GetBytes("hello upload")));
                break;
            case "upload-empty":
                files.Add(new MultipartFile("Picked", "empty.txt", "text/plain", []));
                break;
            // What a browser sends when the file input was left alone: a part with an empty
            // filename rather than no part at all.
            case "upload-none":
                files.Add(new MultipartFile("Picked", "", "application/octet-stream", []));
                break;
            default:
                throw new ArgumentException("Unrecognized upload probe: " + probe);
        }

        using var content = new ByteArrayContent(PostbackFormClient.EncodeMultipart(fields, files));
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
            "multipart/form-data; boundary=" + PostbackFormClient.MultipartBoundary);

        using var response = await client.PostAsync(PostbackFormClient.FormAction(html), content);
        await RecordResponseAsync(options, probe + ":postback", response, index++);

        return index;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string action,
        string body)
    {
        using var content = new StringContent(body, Encoding.UTF8);
        // A browser sends no charset here, so the default request encoding decides the reading.
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        return await client.PostAsync(action, content);
    }

    // Stays valid base64, so the payload reaches MAC validation rather than failing to decode,
    // which is a different rejection.
    private static string FlipOneCharacter(string value)
    {
        var characters = value.ToCharArray();
        var middle = characters.Length / 2;
        characters[middle] = characters[middle] == 'A' ? 'B' : 'A';

        return new string(characters);
    }

    private static async Task RunBodyProbeAsync(
        HttpClient client,
        ScenarioOptions options,
        string probe,
        int index)
    {
        if (probe == "abort")
        {
            await AbortBodyAsync(client.BaseAddress!, useApm: false);
            TraceJournal.Record("request:abort:client-closed");
            return;
        }

        if (probe == "abort-apm")
        {
            await AbortBodyAsync(client.BaseAddress!, useApm: true);
            TraceJournal.Record("request:abort-apm:client-closed");
            return;
        }

        var mode = probe switch
        {
            "fixed-input" => "input",
            "fixed-binary" => "binary",
            "fixed-buffered" => "buffered",
            "fixed-bufferless" => "bufferless",
            "spill" => "spill",
            "too-large" => "input",
            "chunked-too-large" => "input",
            "kestrel-too-large" => "input",
            "chunked-kestrel-too-large" => "input",
            "chunked-customerrors-kestrel-too-large" => "input",
            "preload-delayed" => "preload",
            "unread" => "unread",
            "chunked-apm" => "apm",
            _ => "bufferless",
        };
        var path = "/body?mode=" + mode;
        var payload = probe switch
        {
            "spill" => Enumerable.Repeat((byte)'s', 2048).ToArray(),
            "too-large" => Enumerable.Repeat((byte)'l', 5000).ToArray(),
            "chunked-too-large" => Enumerable.Repeat((byte)'c', 5000).ToArray(),
            "kestrel-too-large" => Enumerable.Repeat((byte)'k', 2048).ToArray(),
            "chunked-kestrel-too-large" => Enumerable.Repeat((byte)'K', 2048).ToArray(),
            "chunked-customerrors-kestrel-too-large" => Enumerable.Repeat((byte)'C', 2048).ToArray(),
            _ => Encoding.UTF8.GetBytes("body:" + probe),
        };

        if (probe == "expect-continue")
        {
            await ExpectContinueAsync(client.BaseAddress!, options, payload, index);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = probe.StartsWith("chunked-", StringComparison.Ordinal)
                || probe == "preload-delayed"
                    ? new DelayedChunkedContent(payload)
                    : new ByteArrayContent(payload),
        };

        using var response = await client.SendAsync(request);
        await RecordResponseAsync(options, probe, response, index);
    }

    // The deadline is deliberately generous: reset-detection latency is load-dependent (observed
    // 0.8s-8.1s under suite load on 4 vCPUs) and is recorded in the trace, never asserted.
    private static async Task AbortBodyAsync(Uri address, bool useApm)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(address.Host, address.Port, timeout.Token);
        await using var stream = client.GetStream();
        var headers = EncodeHttpHeaders(
            $$"""
            POST /body?mode={{(useApm ? "abort-apm" : "abort")}} HTTP/1.1
            Host: {{address.Authority}}
            Content-Length: 100
            Expect: 100-continue
            """);
        await stream.WriteAsync(headers, timeout.Token);
        await stream.FlushAsync(timeout.Token);

        var interim = await ReadResponseHeadAsync(stream, timeout.Token);
        if (interim.StatusCode != 100)
        {
            throw new InvalidDataException(
                "Expected HTTP 100 Continue, received " + interim.StatusCode + ".");
        }

        TraceJournal.Record((useApm ? "body-apm" : "body") + "-abort-interim:100");
        await stream.WriteAsync("partial"u8.ToArray(), timeout.Token);
        await stream.FlushAsync(timeout.Token);
        client.Client.LingerState = new System.Net.Sockets.LingerOption(true, 0);
        client.Close();

        var detection = System.Diagnostics.Stopwatch.StartNew();

        while (!timeout.IsCancellationRequested)
        {
            var tracePath = Environment.GetEnvironmentVariable(TraceJournal.TraceVariable)!;
            var marker = useApm ? "body-apm-abort:" : "body-abort:";
            if (File.Exists(tracePath)
                && File.ReadAllText(tracePath).Contains(marker, StringComparison.Ordinal))
            {
                TraceJournal.Record(
                    (useApm ? "body-apm" : "body")
                    + "-abort-latency-ms:"
                    + detection.ElapsedMilliseconds);
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("The aborted request-body read did not complete.");
    }

    private static async Task ExpectContinueAsync(
        Uri address,
        ScenarioOptions options,
        byte[] body,
        int index)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync(address.Host, address.Port, timeout.Token);
        await using var stream = client.GetStream();
        var headers = EncodeHttpHeaders(
            $$"""
            POST /body?mode=bufferless HTTP/1.1
            Host: {{address.Authority}}
            Content-Length: {{body.Length}}
            Expect: 100-continue
            """);

        await stream.WriteAsync(headers, timeout.Token);
        await stream.FlushAsync(timeout.Token);

        var interim = await ReadResponseHeadAsync(stream, timeout.Token);
        if (interim.StatusCode != 100)
        {
            throw new InvalidDataException(
                "Expected HTTP 100 Continue, received " + interim.StatusCode + ".");
        }

        TraceJournal.Record("expect-interim:100");
        await stream.WriteAsync(body, timeout.Token);
        await stream.FlushAsync(timeout.Token);

        var final = await ReadResponseHeadAsync(stream, timeout.Token);
        var contentLength = final.ContentLength
            ?? throw new InvalidDataException("The final response has no Content-Length.");
        var responseBody = new byte[contentLength];
        await ReadExactlyAsync(stream, responseBody, timeout.Token);

        if (options.ResponseDirectory != null)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(options.ResponseDirectory, index + ".body"),
                responseBody);
        }

        TraceJournal.Record("request:expect-continue:" + final.StatusCode);
        final.RecordHeader("Content-Type", "content-type");
        final.RecordHeader("X-Remote-Port", "x-remote-port");
    }

    private static async Task<RawResponseHead> ReadResponseHeadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var current = new byte[1];

        while (bytes.Count < 64 * 1024)
        {
            if (await stream.ReadAsync(current, cancellationToken) == 0)
            {
                throw new EndOfStreamException("The HTTP response ended before its headers.");
            }

            bytes.Add(current[0]);
            var count = bytes.Count;
            if (count >= 4
                && bytes[count - 4] == '\r'
                && bytes[count - 3] == '\n'
                && bytes[count - 2] == '\r'
                && bytes[count - 1] == '\n')
            {
                return RawResponseHead.Parse(Encoding.ASCII.GetString(bytes.ToArray()));
            }
        }

        throw new InvalidDataException("The HTTP response headers exceeded 64 KB.");
    }

    private static byte[] EncodeHttpHeaders(string headers)
    {
        var terminated = headers.ReplaceLineEndings("\r\n") + "\r\n\r\n";
        return Encoding.ASCII.GetBytes(terminated);
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken);
            if (count == 0)
            {
                throw new EndOfStreamException("The HTTP response body ended early.");
            }

            offset += count;
        }
    }

    private static async Task RecordResponseAsync(
        ScenarioOptions options,
        string label,
        HttpResponseMessage response,
        int index)
    {
        var body = await response.Content.ReadAsByteArrayAsync();
        if (options.ResponseDirectory != null)
        {
            await File.WriteAllBytesAsync(
                Path.Combine(options.ResponseDirectory, index + ".body"),
                body);
        }

        TraceJournal.Record("request:" + label + ":" + (int)response.StatusCode);
        TraceJournal.Record("content-type:" + response.Content.Headers.ContentType);
        if ((int)response.StatusCode >= 500)
        {
            var error = Regex.Replace(Encoding.UTF8.GetString(body), @"\s+", " ");
            TraceJournal.Record(
                "error-body:" + error.Substring(0, Math.Min(1000, error.Length)));
        }
        RecordHeader(response, "X-Remote-Port");
        RecordHeader(response, "X-Read-Mode");
        RecordHeader(response, "X-Spilled");
    }

    private static void RecordHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            TraceJournal.Record(name.ToLowerInvariant() + ":" + string.Join(",", values));
        }
    }

    private static void HoldUntilReleased(string gateName)
    {
        // A named mutex is the one named synchronization object supported on every platform here;
        // named events and semaphores throw off Windows.
        using var gate = new Mutex(false, gateName);
        var acquired = false;

        TraceJournal.Record("holding");
        try
        {
            acquired = gate.WaitOne(TimeSpan.FromMinutes(2));
        }
        catch (AbandonedMutexException)
        {
            // The caller died holding the gate; releasing this process is the useful response.
            acquired = true;
        }

        if (acquired)
        {
            gate.ReleaseMutex();
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    private sealed class DelayedChunkedContent(byte[] payload) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            var first = payload.Length / 2;
            await stream.WriteAsync(payload.AsMemory(0, first));
            await stream.FlushAsync();
            await Task.Delay(50);
            await stream.WriteAsync(payload.AsMemory(first));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class RawResponseHead(
        int statusCode,
        Dictionary<string, string> headers)
    {
        internal int StatusCode { get; } = statusCode;

        internal int? ContentLength =>
            headers.TryGetValue("Content-Length", out var value)
                ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
                : null;

        internal static RawResponseHead Parse(string value)
        {
            var lines = value.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var status = lines[0].Split(' ');
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 1; i < lines.Length && lines[i].Length != 0; i++)
            {
                var separator = lines[i].IndexOf(':');
                if (separator > 0)
                {
                    headers[lines[i].Substring(0, separator)] = lines[i]
                        .Substring(separator + 1)
                        .Trim();
                }
            }

            return new RawResponseHead(
                int.Parse(status[1], System.Globalization.CultureInfo.InvariantCulture),
                headers);
        }

        internal void RecordHeader(string name, string traceName)
        {
            if (headers.TryGetValue(name, out var value))
            {
                TraceJournal.Record(traceName + ":" + value);
            }
        }
    }
}

internal sealed class ScenarioOptions
{
    private ScenarioOptions(
        string applicationId,
        string applicationPath,
        string compilationTempDirectory,
        string tracePath,
        string? holdGate,
        string? responseDirectory,
        string? machineConfigurationPath,
        bool serve,
        List<string> requests,
        List<string> bodyProbes,
        List<string> postbacks)
    {
        MachineConfigurationPath = machineConfigurationPath;
        Serve = serve;
        HoldGate = holdGate;
        ApplicationId = applicationId;
        ApplicationPath = applicationPath;
        CompilationTempDirectory = compilationTempDirectory;
        TracePath = tracePath;
        ResponseDirectory = responseDirectory;
        Requests = requests;
        BodyProbes = bodyProbes;
        Postbacks = postbacks;
    }

    internal string ApplicationId { get; }

    internal string ApplicationPath { get; }

    internal string CompilationTempDirectory { get; }

    internal string TracePath { get; }

    internal string? HoldGate { get; }

    internal string? ResponseDirectory { get; }

    internal string? MachineConfigurationPath { get; }

    internal bool Serve { get; }

    internal List<string> Requests { get; }

    internal List<string> BodyProbes { get; }

    internal List<string> Postbacks { get; }

    internal static ScenarioOptions Parse(string[] args)
    {
        var applicationId = "scenario";
        string? applicationPath = null;
        string? compilationTempDirectory = null;
        string? tracePath = null;
        string? holdGate = null;
        string? responseDirectory = null;
        string? machineConfigurationPath = null;
        var serve = false;
        var requests = new List<string>();
        var bodyProbes = new List<string>();
        var postbacks = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--app":
                    applicationPath = Require(value, "--app");
                    i++;
                    break;
                case "--temp":
                    compilationTempDirectory = Require(value, "--temp");
                    i++;
                    break;
                case "--trace":
                    tracePath = Require(value, "--trace");
                    i++;
                    break;
                case "--id":
                    applicationId = Require(value, "--id");
                    i++;
                    break;
                case "--hold-gate":
                    holdGate = Require(value, "--hold-gate");
                    i++;
                    break;
                case "--response-dir":
                    responseDirectory = Path.GetFullPath(Require(value, "--response-dir"));
                    i++;
                    break;
                case "--machine-config":
                    machineConfigurationPath = Path.GetFullPath(Require(value, "--machine-config"));
                    i++;
                    break;
                case "--serve":
                    serve = true;
                    break;
                case "--request":
                    requests.Add(Require(value, "--request"));
                    i++;
                    break;
                case "--body-probe":
                    bodyProbes.Add(Require(value, "--body-probe"));
                    i++;
                    break;
                case "--postback":
                    postbacks.Add(Require(value, "--postback"));
                    i++;
                    break;
                default:
                    throw new ArgumentException("Unrecognized argument: " + args[i]);
            }
        }

        if (requests.Count == 0)
        {
            requests.Add("/default");
        }

        return new ScenarioOptions(
            applicationId,
            Path.GetFullPath(Require(applicationPath, "--app")),
            Path.GetFullPath(Require(compilationTempDirectory, "--temp")),
            Path.GetFullPath(Require(tracePath, "--trace")),
            holdGate,
            responseDirectory,
            machineConfigurationPath,
            serve,
            requests,
            bodyProbes,
            postbacks);
    }

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(name + " is required.");
        }

        return value;
    }
}

// ApplicationManager hands the application a registered object, which is the seam through which
// requests enter an activated application.
public sealed class ScenarioRunner : MarshalByRefObject, IRegisteredObject
{
    public int Request(string path, string? responsePath)
    {
        var request = new ScenarioWorkerRequest(path);
        HttpRuntime.ProcessRequest(request);

        if (!request.WaitForCompletion(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The request did not complete: " + path);
        }

        if (responsePath != null)
        {
            File.WriteAllBytes(responsePath, request.BodyBytes);
        }

        if (request.StatusCode >= 500)
        {
            TraceJournal.Record("error-body:" + Summarize(request.Body));
        }

        return request.StatusCode;
    }

    public void Stop(bool immediate)
    {
        HostingEnvironment.UnregisterObject(this);
    }

    // A compilation error page is thousands of characters of markup; the compiler diagnostics in
    // it are what a failing scenario needs to report.
    private static string Summarize(string body)
    {
        // The style block alone is longer than anything worth recording, and it sits ahead of the
        // compiler diagnostics that make a failing scenario diagnosable.
        var text = Regex.Replace(body, "<(style|script)[^>]*>.*?</\\1>", " ", RegexOptions.Singleline);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text.Length > 600 ? text[..600] : text;
    }
}
