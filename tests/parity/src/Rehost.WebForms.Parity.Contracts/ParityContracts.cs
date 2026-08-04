using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Rehost.WebForms.Parity.Contracts;

// Requests reach the adapter over HTTP rather than through a call into the application, so it
// implements only the drain half.
public interface IPipelineEventDrain
{
    List<string> DrainApplicationEvents();

    List<string> DrainSessionEvents();
}

public interface IClassicPipelineRunner : IPipelineEventDrain
{
    List<RequestObservation> RunStep(List<RequestSpecification> requests);
}

[Serializable]
[DataContract]
public sealed class RequestSpecification
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";

    [DataMember(Order = 2)]
    public string Method { get; set; } = "GET";

    [DataMember(Order = 3)]
    public string Path { get; set; } = "/oracle";

    [DataMember(Order = 4)]
    public string QueryString { get; set; } = "";

    [DataMember(Order = 5)]
    public string BodyBase64 { get; set; } = "";

    [DataMember(Order = 6)]
    public string BodyFraming { get; set; } = "";

    [DataMember(Order = 7)]
    public int PreloadedBodyLength { get; set; }
}

[Serializable]
[DataContract]
public sealed class SessionSpecification
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";

    [DataMember(Order = 2)]
    public string Fixture { get; set; } = "";

    // Each step's requests are issued together; a step of one is sequential.
    [DataMember(Order = 3)]
    public List<List<RequestSpecification>> Steps { get; set; }
        = new List<List<RequestSpecification>>();
}

[DataContract]
public sealed class SessionManifest
{
    [DataMember(Order = 1)]
    public int SchemaVersion { get; set; }

    [DataMember(Order = 2)]
    public List<SessionSpecification> Sessions { get; set; } = new List<SessionSpecification>();
}

[Serializable]
[DataContract]
public sealed class PipelineObservation
{
    [DataMember(Order = 1)]
    public List<string> Events { get; set; } = new List<string>();

    [DataMember(Order = 2)]
    public ResponseObservation Response { get; set; } = new ResponseObservation();

    [DataMember(Order = 3)]
    public ExceptionObservation? EscapedException { get; set; }

    [DataMember(Order = 4)]
    public int EndOfRequestCount { get; set; }

    [DataMember(Order = 5)]
    public int CompletionCount { get; set; }
}

[Serializable]
[DataContract]
public sealed class ResponseObservation
{
    [DataMember(Order = 1)]
    public int StatusCode { get; set; }

    [DataMember(Order = 2)]
    public string StatusDescription { get; set; } = "";

    [DataMember(Order = 3)]
    public List<HeaderObservation> Headers { get; set; } = new List<HeaderObservation>();

    [DataMember(Order = 4)]
    public string BodyBase64 { get; set; } = "";

    [DataMember(Order = 5)]
    public List<bool> Flushes { get; set; } = new List<bool>();
}

[Serializable]
[DataContract]
public sealed class HeaderObservation
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";

    [DataMember(Order = 2)]
    public string Value { get; set; } = "";
}

[Serializable]
[DataContract]
public sealed class ExceptionObservation
{
    [DataMember(Order = 1)]
    public string Type { get; set; } = "";

    [DataMember(Order = 2)]
    public string Message { get; set; } = "";

    [DataMember(Order = 3)]
    public int HResult { get; set; }

    [DataMember(Order = 4)]
    public ExceptionObservation? Inner { get; set; }

    public static ExceptionObservation FromException(Exception exception)
    {
        return new ExceptionObservation
        {
            Type = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            HResult = exception.HResult,
            Inner = exception.InnerException == null
                ? null
                : FromException(exception.InnerException)
        };
    }
}

[Serializable]
[DataContract]
public sealed class RequestObservation
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";

    [DataMember(Order = 2)]
    public PipelineObservation Observation { get; set; } = new PipelineObservation();
}

[DataContract]
public sealed class SessionObservation
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";

    // Application instances are constructed concurrently, so this collection is a bag: its
    // order carries no claim and is canonicalized by sorting. Counts still compare exactly.
    [DataMember(Order = 2)]
    public List<string> ApplicationEvents { get; set; } = new List<string>();

    [DataMember(Order = 3)]
    public List<string> SessionEvents { get; set; } = new List<string>();

    [DataMember(Order = 4)]
    public List<RequestObservation> Requests { get; set; } = new List<RequestObservation>();
}

[DataContract]
public sealed class PipelineTrace
{
    [DataMember(Order = 1)]
    public int SchemaVersion { get; set; }

    [DataMember(Order = 2)]
    public TraceProvenance Provenance { get; set; } = new TraceProvenance();

    [DataMember(Order = 3)]
    public List<SessionObservation> Sessions { get; set; } = new List<SessionObservation>();
}

[DataContract]
public sealed class TraceProvenance
{
    [DataMember(Order = 1)]
    public string Oracle { get; set; } = "";

    [DataMember(Order = 2)]
    public string TargetFramework { get; set; } = "";

    [DataMember(Order = 3)]
    public string RuntimeRequirement { get; set; } = "";

    [DataMember(Order = 4)]
    public string ManagedEntryPoint { get; set; } = "";

    [DataMember(Order = 5)]
    public string ActivationEntryPoint { get; set; } = "";

    [DataMember(Order = 6)]
    public string Fixture { get; set; } = "";
}

// Concurrent requests would interleave a single shared list, so each request owns a notebook
// and anything belonging to the application rather than one request goes to the session
// notebook. A session is always a fresh process, so the statics start empty.
public static class PipelineEventJournal
{
    public const string RequestHeaderName = "X-Parity-Request";

    private static readonly object Sync = new object();
    private static readonly List<string> Application = new List<string>();
    private static readonly List<string> Session = new List<string>();
    private static readonly Dictionary<string, List<string>> Requests
        = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    private static int _applicationsCreated;

    public static int ApplicationsCreated
    {
        get
        {
            lock (Sync)
            {
                return _applicationsCreated;
            }
        }
    }

    public static void CountApplication()
    {
        lock (Sync)
        {
            _applicationsCreated++;
        }
    }

    public static void OpenRequest(string requestName)
    {
        lock (Sync)
        {
            Requests[requestName] = new List<string>();
        }
    }

    public static void RecordApplication(string value)
    {
        lock (Sync)
        {
            Application.Add(value);
        }
    }

    public static void RecordSession(string value)
    {
        lock (Sync)
        {
            Session.Add(value);
        }
    }

    public static void Record(string? requestName, string value)
    {
        lock (Sync)
        {
            if (requestName != null && Requests.TryGetValue(requestName, out var events))
            {
                events.Add(value);
                return;
            }

            Session.Add(value);
        }
    }

    public static List<string> DrainRequest(string requestName)
    {
        lock (Sync)
        {
            if (!Requests.TryGetValue(requestName, out var events))
            {
                return new List<string>();
            }

            Requests.Remove(requestName);
            return events;
        }
    }

    // Sorted, not chronological: instances initialize on overlapping threads, so the recorded
    // order is thread scheduling rather than behavior. Sorting keeps every line and its count
    // while making the collection reproducible.
    public static List<string> DrainApplication()
    {
        lock (Sync)
        {
            var drained = new List<string>(Application);
            Application.Clear();
            drained.Sort(StringComparer.Ordinal);
            return drained;
        }
    }

    public static List<string> DrainSession()
    {
        lock (Sync)
        {
            var drained = new List<string>(Session);
            Session.Clear();
            return drained;
        }
    }
}
