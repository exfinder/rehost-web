using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CoreParity.Contracts;

public interface IClassicPipelineRunner
{
    PipelineObservation Run(RequestSpecification request);

    List<string> DrainEvents();
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
}

[Serializable]
[DataContract]
public sealed class SessionSpecification
{
    [DataMember(Order = 1)]
    public string Name { get; set; } = "";

    [DataMember(Order = 2)]
    public string Fixture { get; set; } = "";

    [DataMember(Order = 3)]
    public List<RequestSpecification> Requests { get; set; } = new List<RequestSpecification>();
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

    [DataMember(Order = 2)]
    public List<RequestObservation> Requests { get; set; } = new List<RequestObservation>();

    [DataMember(Order = 3)]
    public List<string> TrailingEvents { get; set; } = new List<string>();
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

public static class PipelineEventJournal
{
    private static readonly object Sync = new object();
    private static readonly List<string> Events = new List<string>();

    public static void Reset()
    {
        lock (Sync)
        {
            Events.Clear();
        }
    }

    public static void Record(string value)
    {
        lock (Sync)
        {
            Events.Add(value);
        }
    }

    public static List<string> Drain()
    {
        lock (Sync)
        {
            var drained = new List<string>(Events);
            Events.Clear();
            return drained;
        }
    }
}
