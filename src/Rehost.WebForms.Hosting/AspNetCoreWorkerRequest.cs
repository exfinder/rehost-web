namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using HttpException = System.Web.HttpException;
using System.Web.Hosting;
using System.Web.WebSockets;
using HttpWorkerRequest = System.Web.HttpWorkerRequest;

internal sealed class AspNetCoreWorkerRequest : HttpWorkerRequest, IDisposable
{
    private readonly HttpContext _context;
    private readonly string _virtualRootPath;
    private readonly string _physicalRootPath;
    private readonly RequestBodyCoordinator _body;
    private readonly bool _canHaveBody;
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _uriPath;
    private (string FilePath, string PathInfo)? _split;
    private bool _statusSent;
    private bool _clientGone;
    private (System.Web.HttpContext Context, Func<AspNetWebSocketContext, Task> UserFunc, string? SubProtocol)? _webSocketAccept;

    internal AspNetCoreWorkerRequest(
        HttpContext context,
        string virtualRootPath,
        string physicalRootPath,
        Func<string> temporaryDirectoryAccessor)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(virtualRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(physicalRootPath);

        _context = context;
        _virtualRootPath = NormalizeVirtualRoot(virtualRootPath);
        _physicalRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(physicalRootPath));
        _body = new RequestBodyCoordinator(context.Request.BodyReader, context.RequestAborted);
        _canHaveBody = context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true;
        Response = new ResponseSpool(temporaryDirectoryAccessor);
    }

    internal ResponseSpool Response { get; }

    // Set by AcceptWebSocketRequest; the middleware hands the connection over once the pipeline
    // has finished with a 101.
    internal (System.Web.HttpContext Context, Func<AspNetWebSocketContext, Task> UserFunc, string? SubProtocol)? WebSocketAccept =>
        _webSocketAccept;

    internal override bool SupportsWebSocketUpgrade => true;

    // Framework's own "module not enabled" refusal when the WebSocket middleware is absent.
    internal override bool IsWebSocketUpgradeRequest()
    {
        var feature = _context.Features.Get<IHttpWebSocketFeature>()
            ?? throw new PlatformNotSupportedException(
                System.Web.SR.GetString(System.Web.SR.WebSockets_WebSocketModuleNotEnabled));
        return feature.IsWebSocketRequest;
    }

    internal override void AcceptWebSocketUpgrade(
        System.Web.HttpContext context,
        Func<AspNetWebSocketContext, Task> userFunc,
        string subProtocol)
    {
        _webSocketAccept = (context, userFunc, subProtocol);
    }

    // Kestrel signals the token on FIN or RST; IIS waited for the server's next write (IV24).
    internal override bool TryGetClientDisconnectedToken(out CancellationToken token)
    {
        token = _context.RequestAborted;
        return true;
    }

    // IV25: the reset does not end the request — managed code keeps running and only a flush
    // surfaces the loss.
    internal override bool TryAbortConnection()
    {
        _clientGone = true;
        _context.Abort();
        return true;
    }

    internal Task Completion => _completion.Task;

    public override string GetUriPath()
    {
        if (_uriPath == null)
        {
            var path = _context.Request.PathBase.Add(_context.Request.Path).Value;
            _uriPath = string.IsNullOrEmpty(path)
                ? "/"
                : RequestPathCanonicalizer.Canonicalize(path, out _);
        }

        return _uriPath;
    }

    public override string GetQueryString()
    {
        var query = _context.Request.QueryString.Value;
        return string.IsNullOrEmpty(query) ? "" : query.TrimStart('?');
    }

    // http.sys handed ASP.NET the canonical, decoded path as the "raw" URL and only the query
    // verbatim (IIS reading, ledger P72).
    public override string GetRawUrl()
    {
        var queryString = GetQueryString();
        return queryString.Length == 0 ? GetUriPath() : GetUriPath() + "?" + queryString;
    }

    public override string GetHttpVerbName()
    {
        return _context.Request.Method;
    }

    public override string GetHttpVersion()
    {
        return _context.Request.Protocol;
    }

    public override string GetRemoteAddress()
    {
        return _context.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    public override int GetRemotePort()
    {
        return _context.Connection.RemotePort;
    }

    public override string GetLocalAddress()
    {
        return _context.Connection.LocalIpAddress?.ToString() ?? "";
    }

    // IIS answered SERVER_PORT with the listening port and Request.Url is built from it; behind a
    // TLS-terminating proxy the port the client used is the Host header's, which the forwarded
    // headers restore, so the header decides and the socket is the fallback.
    public override int GetLocalPort()
    {
        return _context.Request.Host.Port ?? (_context.Request.IsHttps ? 443 : 80);
    }

    // The Host header's host, as IIS reported SERVER_NAME and built Request.Url (reading R1: a
    // request with Host: shop.example.com answered Url=http://shop.example.com:8112/...).
    public override string GetServerName()
    {
        var host = _context.Request.Host.Host;
        return string.IsNullOrEmpty(host) ? GetLocalAddress() : host;
    }

    public override bool IsSecure()
    {
        return _context.Request.IsHttps;
    }

    public override string GetAppPath()
    {
        return _virtualRootPath;
    }

    public override string GetAppPathTranslated()
    {
        return _physicalRootPath + Path.DirectorySeparatorChar;
    }

    // Path info requires resolving where the handler's path ends and the trailing segments begin,
    // which the first slice does not do; every request maps wholly to its file path.
    public override string GetPathInfo()
    {
        return Split.PathInfo;
    }

    public override string GetFilePath()
    {
        return Split.FilePath;
    }

    private (string FilePath, string PathInfo) Split =>
        _split ??= RequestPathInfo.Split(GetHttpVerbName(), GetUriPath());

    public override string? GetFilePathTranslated()
    {
        return MapPath(GetFilePath());
    }

    public override string? MapPath(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath) || virtualPath == "/")
        {
            return _physicalRootPath;
        }

        if (virtualPath[0] != '/' || virtualPath.IndexOf('\\') >= 0)
        {
            return null;
        }

        var relative = StripVirtualRoot(virtualPath);
        if (relative == null)
        {
            return null;
        }

        if (relative.Length == 0)
        {
            return _physicalRootPath;
        }

        var mapped = Path.GetFullPath(
            Path.Combine(_physicalRootPath, relative.Replace('/', Path.DirectorySeparatorChar)));

        return IsWithinApplication(mapped) ? mapped : null;
    }

    public override string? GetKnownRequestHeader(int index)
    {
        return ReadHeader(GetKnownRequestHeaderName(index));
    }

    public override string? GetUnknownRequestHeader(string name)
    {
        return ReadHeader(name);
    }

    public override string[][] GetUnknownRequestHeaders()
    {
        var unknown = new List<string[]>();

        foreach (var header in _context.Request.Headers)
        {
            if (GetKnownRequestHeaderIndex(header.Key) < 0)
            {
                unknown.Add(new[] { header.Key, JoinValues(header.Value) });
            }
        }

        return unknown.ToArray();
    }

    // HttpRequest fills its collection from the worker-request members for most names and asks
    // here only for the IIS-native ones; every one of those was present on IIS (reading R1: 45
    // variables, unset ones ""), so none is null. Site topology takes IIS's shape for a single
    // site; the certificate and TLS-strength set is "" as on an IIS site without client
    // certificates (the negotiated strengths have no non-obsolete source on Kestrel).
    // WEBSOCKET_VERSION is the exception and must stay out of that collection: integrated answers
    // it here while AllKeys omits it and Count stays 45 (IV23).
    public override string? GetServerVariable(string name)
    {
        return name switch
        {
            "APPL_MD_PATH" => "/LM/W3SVC/1/ROOT",
            "INSTANCE_ID" => "1",
            "INSTANCE_META_PATH" => "/LM/W3SVC/1",
            "GATEWAY_INTERFACE" => "CGI/1.1",
            "SERVER_SOFTWARE" => "Kestrel",
            "HTTPS" => IsSecure() ? "on" : "off",
            "REMOTE_PORT" => GetRemotePort().ToString(CultureInfo.InvariantCulture),
            "WEBSOCKET_VERSION" => "13",
            "AUTH_PASSWORD" or "LOGON_USER"
                or "CERT_COOKIE" or "CERT_FLAGS" or "CERT_ISSUER" or "CERT_KEYSIZE"
                or "CERT_SECRETKEYSIZE" or "CERT_SERIALNUMBER" or "CERT_SERVER_ISSUER"
                or "CERT_SERVER_SUBJECT" or "CERT_SUBJECT"
                or "HTTPS_KEYSIZE" or "HTTPS_SECRETKEYSIZE"
                or "HTTPS_SERVER_ISSUER" or "HTTPS_SERVER_SUBJECT" => "",
            _ => name.StartsWith("HTTP_", StringComparison.Ordinal)
                ? ReadHeader(name.Substring(5).Replace('_', '-'))
                : null,
        };
    }

    public override bool IsClientConnected()
    {
        return !_clientGone
            && !_context.RequestAborted.IsCancellationRequested
            && !_body.ClientDisconnected;
    }

    public override byte[]? GetPreloadedEntityBody()
    {
        return null;
    }

    public override int GetPreloadedEntityBodyLength()
    {
        return 0;
    }

    public override int GetPreloadedEntityBody(byte[] buffer, int offset)
    {
        return 0;
    }

    public override bool IsEntireEntityBodyIsPreloaded()
    {
        return !_canHaveBody;
    }

    public override int ReadEntityBody(byte[] buffer, int size)
    {
        return _body.Read(buffer, 0, size);
    }

    public override int ReadEntityBody(byte[] buffer, int offset, int size)
    {
        return _body.Read(buffer, offset, size);
    }

    public override bool SupportsAsyncRead => true;

    public override IAsyncResult BeginRead(
        byte[] buffer,
        int offset,
        int count,
        AsyncCallback callback,
        object state)
    {
        return _body.BeginRead(buffer, offset, count, callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return _body.EndRead(asyncResult);
    }

    public override void SendStatus(int statusCode, string statusDescription)
    {
        _statusSent = true;
        Response.SetStatus(statusCode, statusDescription);
    }

    public override void SendKnownResponseHeader(int index, string value)
    {
        Response.AddHeader(GetKnownResponseHeaderName(index), value);
    }

    public override void SendUnknownResponseHeader(string name, string value)
    {
        Response.AddHeader(name, value);
    }

    public override void SendCalculatedContentLength(int contentLength)
    {
        SendCalculatedContentLength((long)contentLength);
    }

    public override void SendCalculatedContentLength(long contentLength)
    {
        if (_webSocketAccept == null)
        {
            Response.SetContentLength(contentLength);
        }
    }

    // Body bytes written after AcceptWebSocketRequest are discarded: IIS put them on the wire
    // between the 101 and the first frame (reading R-WS2), which no client can parse.
    public override void SendResponseFromMemory(byte[] data, int length)
    {
        if (_webSocketAccept == null)
        {
            Response.Write(data, length);
        }
    }

    public override void SendResponseFromFile(string filename, long offset, long length)
    {
        if (_webSocketAccept == null)
        {
            Response.WriteFile(filename, offset, length);
        }
    }

    internal override bool SupportsLongTransmitFile => true;

    public override void SendResponseFromFile(IntPtr handle, long offset, long length)
    {
        throw new NotSupportedException(
            "Sending a response from a native file handle is not supported on this host.");
    }

    // Framework's Flush put the headers and everything buffered on the wire at once (reading
    // R-S1: 200 + Transfer-Encoding: chunked + the first chunk in one packet). Response.End's
    // deferred flush arrives here before any header was generated (P55) and must publish
    // nothing; the final flush leaves the commit to the middleware, off the pipeline thread.
    public override void FlushResponse(bool finalFlush)
    {
        if (finalFlush || !CanDeliver)
        {
            return;
        }

        Deliver(() => Response.FlushAsync(_context, _context.RequestAborted));
    }

    // A flush delivers only once System.Web has produced its status and headers, and never after
    // a WebSocket accept has claimed the response body.
    private bool CanDeliver => _statusSent && _webSocketAccept == null;

    public override bool SupportsAsyncFlush => true;

    public override IAsyncResult BeginFlush(AsyncCallback callback, object state)
    {
        var flush = CanDeliver
            ? DeliverAsync(() => Response.FlushAsync(_context, _context.RequestAborted))
            : Task.CompletedTask;
        return TaskToAsyncResult.Begin(flush, callback, state);
    }

    public override void EndFlush(IAsyncResult asyncResult)
    {
        TaskToAsyncResult.End(asyncResult);
    }

    // The pipeline thread waits for Kestrel's transport to take the bytes, the same trade the
    // request body makes in the other direction. The chain runs with no ambient synchronization
    // context: System.Web's is current on this thread, and the awaits inside ASP.NET Core's
    // sendfile and buffer-drain helpers would post their continuations to it — behind the very
    // thread waiting here (measured: a TransmitFile between flushes hung on Windows, where the
    // file read completes asynchronously). A transport failure is what Framework's flush raised
    // on a vanished client: an HttpException into the page, the connection gone.
    private void Deliver(Func<Task> flush)
    {
        DeliverAsync(flush).GetAwaiter().GetResult();
    }

    private Task DeliverAsync(Func<Task> flush)
    {
        var context = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return DeliverCoreAsync(flush());
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(context);
        }
    }

    private async Task DeliverCoreAsync(Task flush)
    {
        try
        {
            await flush.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException
            or ObjectDisposedException)
        {
            _clientGone = true;
            _context.Abort();
            throw new HttpException("The remote host closed the connection.", exception);
        }
    }

    public override void EndOfRequest()
    {
        _body.Stop();
        Response.Seal();

        if (!_completion.TrySetResult())
        {
            throw new InvalidOperationException(
                "EndOfRequest was raised more than once for a single request.");
        }
    }

    internal void FailCompletion(Exception exception)
    {
        _completion.TrySetException(exception);
    }

    public void Dispose()
    {
        _body.Stop();
        Response.Dispose();
    }

    private static string NormalizeVirtualRoot(string virtualRootPath)
    {
        var normalized = virtualRootPath.Trim();
        if (normalized[0] != '/')
        {
            throw new ArgumentException(
                $"The virtual application root must be absolute: '{virtualRootPath}'.",
                nameof(virtualRootPath));
        }

        return normalized.Length > 1
            ? normalized.TrimEnd('/')
            : "/";
    }

    private static string JoinValues(StringValues values)
    {
        return values.Count == 1 ? values[0] ?? "" : string.Join(", ", values.ToArray());
    }

    private string? ReadHeader(string? name)
    {
        return !string.IsNullOrEmpty(name)
            && _context.Request.Headers.TryGetValue(name, out var values)
                ? JoinValues(values)
                : null;
    }

    private string? StripVirtualRoot(string virtualPath)
    {
        if (_virtualRootPath == "/")
        {
            return virtualPath.Substring(1);
        }

        if (!virtualPath.StartsWith(_virtualRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var remainder = virtualPath.Substring(_virtualRootPath.Length);
        if (remainder.Length == 0)
        {
            return "";
        }

        return remainder[0] == '/' ? remainder.Substring(1) : null;
    }

    private bool IsWithinApplication(string mappedPath)
    {
        return mappedPath.Length == _physicalRootPath.Length
            ? string.Equals(mappedPath, _physicalRootPath, StringComparison.Ordinal)
            : mappedPath.StartsWith(_physicalRootPath, StringComparison.Ordinal)
                && mappedPath[_physicalRootPath.Length] == Path.DirectorySeparatorChar;
    }
}
