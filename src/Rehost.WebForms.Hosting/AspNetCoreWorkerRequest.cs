namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
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

    internal Task Completion => _completion.Task;

    public override string GetUriPath()
    {
        var path = _context.Request.PathBase.Add(_context.Request.Path).Value;
        return string.IsNullOrEmpty(path) ? "/" : path;
    }

    public override string GetQueryString()
    {
        var query = _context.Request.QueryString.Value;
        return string.IsNullOrEmpty(query) ? "" : query.TrimStart('?');
    }

    public override string GetRawUrl()
    {
        var rawTarget = _context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        if (!string.IsNullOrEmpty(rawTarget))
        {
            return rawTarget;
        }

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

    public override int GetLocalPort()
    {
        return _context.Connection.LocalPort;
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
        return "";
    }

    public override string GetFilePath()
    {
        return GetUriPath();
    }

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

    // Unsupported variables answer null rather than an empty string: System.Web treats null as
    // "the server does not provide this", while "" claims the server provided nothing.
    public override string? GetServerVariable(string name)
    {
        return name switch
        {
            "SERVER_NAME" => _context.Request.Host.Host,
            "SERVER_PORT" => ServerPort().ToString(CultureInfo.InvariantCulture),
            "SERVER_PORT_SECURE" => _context.Request.IsHttps ? "1" : "0",
            "SERVER_PROTOCOL" => _context.Request.Protocol,
            "REQUEST_METHOD" => _context.Request.Method,
            "QUERY_STRING" => GetQueryString(),
            "SCRIPT_NAME" => GetFilePath(),
            "PATH_INFO" => GetPathInfo(),
            "APPL_PHYSICAL_PATH" => GetAppPathTranslated(),
            "REMOTE_ADDR" or "REMOTE_HOST" => GetRemoteAddress(),
            "REMOTE_PORT" => GetRemotePort().ToString(CultureInfo.InvariantCulture),
            "LOCAL_ADDR" => GetLocalAddress(),
            "HTTPS" => _context.Request.IsHttps ? "on" : "off",
            _ => name.StartsWith("HTTP_", StringComparison.Ordinal)
                ? ReadHeader(name.Substring(5).Replace('_', '-'))
                : null,
        };
    }

    public override bool IsClientConnected()
    {
        return !_context.RequestAborted.IsCancellationRequested;
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
        Response.SetContentLength(contentLength);
    }

    public override void SendCalculatedContentLength(long contentLength)
    {
        Response.SetContentLength(contentLength);
    }

    public override void SendResponseFromMemory(byte[] data, int length)
    {
        Response.Write(data, length);
    }

    public override void SendResponseFromFile(string filename, long offset, long length)
    {
        throw new NotSupportedException(
            "Sending a response from a file is outside the first-slice transport envelope.");
    }

    public override void SendResponseFromFile(IntPtr handle, long offset, long length)
    {
        throw new NotSupportedException(
            "Sending a response from a native file handle is not supported on this host.");
    }

    // The response is committed once, after EndOfRequest seals it, so an intermediate flush has
    // nothing to push. Client-visible streaming is outside the first-slice envelope.
    public override void FlushResponse(bool finalFlush)
    {
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

    private int ServerPort()
    {
        return _context.Request.Host.Port ?? (_context.Request.IsHttps ? 443 : 80);
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
