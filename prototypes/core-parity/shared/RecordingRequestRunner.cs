using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web;
using CoreParity.Contracts;

namespace CoreParity.Recording;

internal sealed class RecordingRequestRunner
{
    internal List<RequestObservation> RunStep(List<RequestSpecification> requests)
    {
        if (requests == null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        if (requests.Count == 0)
        {
            throw new ArgumentException("A step declares no requests.", nameof(requests));
        }

        foreach (var request in requests)
        {
            PipelineEventJournal.OpenRequest(request.Name);
        }

        var observations = new RequestObservation[requests.Count];

        if (requests.Count == 1)
        {
            observations[0] = Run(requests[0]);
            return new List<RequestObservation>(observations);
        }

        var failures = new Exception?[requests.Count];
        // Dedicated threads rather than the pool: a step exists to put requests in flight
        // together, and pool scheduling is free to run them one after another.
        var threads = new Thread[requests.Count];

        for (var index = 0; index < requests.Count; index++)
        {
            var slot = index;
            threads[slot] = new Thread(() =>
            {
                try
                {
                    observations[slot] = Run(requests[slot]);
                }
                catch (Exception exception)
                {
                    failures[slot] = exception;
                }
            })
            {
                IsBackground = true,
                Name = "parity:" + requests[slot].Name
            };
            threads[slot].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        foreach (var failure in failures)
        {
            if (failure != null)
            {
                throw failure;
            }
        }

        return new List<RequestObservation>(observations);
    }

    private static RequestObservation Run(RequestSpecification request)
    {
        var workerRequest = new RecordingWorkerRequest(request);
        ExceptionObservation? escapedException = null;

        PipelineEventJournal.Record(request.Name, "runner.process-request.enter");

        try
        {
            HttpRuntime.ProcessRequest(workerRequest);
            PipelineEventJournal.Record(request.Name, "runner.process-request.return");
        }
        catch (Exception exception)
        {
            escapedException = ExceptionObservation.FromException(exception);
            PipelineEventJournal.Record(request.Name, "runner.process-request.escape");
        }

        if (escapedException == null
            && !workerRequest.WaitForCompletion(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException(
                "EndOfRequest for '"
                + request.Name
                + "' did not complete within 30 seconds.");
        }

        return new RequestObservation
        {
            Name = request.Name,
            Observation = workerRequest.CreateObservation(
                PipelineEventJournal.DrainRequest(request.Name),
                escapedException)
        };
    }
}

internal sealed class RecordingWorkerRequest : HttpWorkerRequest
{
    private readonly RequestSpecification _request;
    private readonly MemoryStream _body = new MemoryStream();
    private readonly ManualResetEvent _completed = new ManualResetEvent(false);
    private readonly List<HeaderObservation> _headers = new List<HeaderObservation>();
    private readonly List<bool> _flushes = new List<bool>();
    private int _statusCode = 200;
    private string _statusDescription = "OK";
    private int _endOfRequestCount;
    private int _completionCount;

    internal RecordingWorkerRequest(RequestSpecification request)
    {
        _request = request;
    }

    public override string GetUriPath()
    {
        return _request.Path;
    }

    public override string GetQueryString()
    {
        return _request.QueryString;
    }

    public override string GetRawUrl()
    {
        return string.IsNullOrEmpty(_request.QueryString)
            ? _request.Path
            : _request.Path + "?" + _request.QueryString;
    }

    public override string GetHttpVerbName()
    {
        return _request.Method;
    }

    public override string GetHttpVersion()
    {
        return "HTTP/1.1";
    }

    public override string GetRemoteAddress()
    {
        return "127.0.0.1";
    }

    public override int GetRemotePort()
    {
        return 49152;
    }

    public override string GetLocalAddress()
    {
        return "127.0.0.1";
    }

    public override int GetLocalPort()
    {
        return 80;
    }

    public override string GetFilePath()
    {
        return _request.Path;
    }

    public override string GetFilePathTranslated()
    {
        var relative = _request.Path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(GetAppPathTranslated(), relative);
    }

    public override string GetPathInfo()
    {
        return "";
    }

    public override string GetAppPath()
    {
        return "/";
    }

    public override string GetAppPathTranslated()
    {
        return HttpRuntime.AppDomainAppPath ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    public override string? GetKnownRequestHeader(int index)
    {
        if (index == HeaderHost)
        {
            return "oracle.invalid";
        }

        return null;
    }

    // Probes run on pipeline threads and identify their request from this header rather than
    // from ambient context, which keeps them clear of unresolved CallContext behavior.
    public override string? GetUnknownRequestHeader(string name)
    {
        return string.Equals(
                name,
                PipelineEventJournal.RequestHeaderName,
                StringComparison.OrdinalIgnoreCase)
            ? _request.Name
            : null;
    }

    public override string[][] GetUnknownRequestHeaders()
    {
        return new[]
        {
            new[] { PipelineEventJournal.RequestHeaderName, _request.Name }
        };
    }

    public override string GetServerVariable(string name)
    {
        if (string.Equals(name, "SERVER_NAME", StringComparison.OrdinalIgnoreCase))
        {
            return "oracle.invalid";
        }

        if (string.Equals(name, "SERVER_PORT", StringComparison.OrdinalIgnoreCase))
        {
            return "80";
        }

        if (string.Equals(name, "HTTPS", StringComparison.OrdinalIgnoreCase))
        {
            return "off";
        }

        return "";
    }

    public override string? MapPath(string virtualPath)
    {
        if (string.IsNullOrEmpty(virtualPath) || virtualPath == "/")
        {
            return GetAppPathTranslated();
        }

        if (!virtualPath.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var relative = virtualPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(GetAppPathTranslated(), relative);
    }

    public override void SendStatus(int statusCode, string statusDescription)
    {
        _statusCode = statusCode;
        _statusDescription = statusDescription;
        PipelineEventJournal.Record(_request.Name, "worker.status");
    }

    public override void SendKnownResponseHeader(int index, string value)
    {
        _headers.Add(new HeaderObservation
        {
            Name = GetKnownResponseHeaderName(index),
            Value = value
        });
        PipelineEventJournal.Record(_request.Name, "worker.header.known");
    }

    public override void SendUnknownResponseHeader(string name, string value)
    {
        _headers.Add(new HeaderObservation
        {
            Name = name,
            Value = value
        });
        PipelineEventJournal.Record(_request.Name, "worker.header.unknown");
    }

    public override void SendCalculatedContentLength(int contentLength)
    {
        _headers.Add(new HeaderObservation
        {
            Name = GetKnownResponseHeaderName(HeaderContentLength),
            Value = contentLength.ToString(CultureInfo.InvariantCulture)
        });
        PipelineEventJournal.Record(_request.Name, "worker.header.content-length");
    }

    public override void SendResponseFromMemory(byte[] data, int length)
    {
        _body.Write(data, 0, length);
        PipelineEventJournal.Record(_request.Name, "worker.body");
    }

    public override void SendResponseFromFile(string filename, long offset, long length)
    {
        using (var stream = File.OpenRead(filename))
        {
            stream.Position = offset;
            CopyBytes(stream, length);
        }

        PipelineEventJournal.Record(_request.Name, "worker.file");
    }

    public override void SendResponseFromFile(IntPtr handle, long offset, long length)
    {
        throw new NotSupportedException(
            "The parity harness does not support native file handles.");
    }

    public override void FlushResponse(bool finalFlush)
    {
        _flushes.Add(finalFlush);
        PipelineEventJournal.Record(
            _request.Name,
            finalFlush ? "worker.flush.final" : "worker.flush");
    }

    public override void EndOfRequest()
    {
        Interlocked.Increment(ref _endOfRequestCount);
        PipelineEventJournal.Record(_request.Name, "worker.end-of-request");

        if (Interlocked.CompareExchange(ref _completionCount, 1, 0) == 0)
        {
            _completed.Set();
        }
    }

    internal bool WaitForCompletion(TimeSpan timeout)
    {
        return _completed.WaitOne(timeout);
    }

    internal PipelineObservation CreateObservation(
        List<string> events,
        ExceptionObservation? escapedException)
    {
        return new PipelineObservation
        {
            Events = events,
            Response = new ResponseObservation
            {
                StatusCode = _statusCode,
                StatusDescription = _statusDescription,
                Headers = new List<HeaderObservation>(_headers),
                BodyBase64 = Convert.ToBase64String(_body.ToArray()),
                Flushes = new List<bool>(_flushes)
            },
            EscapedException = escapedException,
            EndOfRequestCount = _endOfRequestCount,
            CompletionCount = _completionCount
        };
    }

    private void CopyBytes(Stream source, long length)
    {
        var buffer = new byte[8192];
        var remaining = length;

        while (remaining > 0)
        {
            var read = source.Read(
                buffer,
                0,
                (int)Math.Min(buffer.Length, remaining));

            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            _body.Write(buffer, 0, read);
            remaining -= read;
        }
    }
}
