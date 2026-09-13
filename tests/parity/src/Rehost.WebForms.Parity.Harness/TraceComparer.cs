using System;
using System.Collections.Generic;
using System.Linq;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Harness;

public static class TraceComparer
{
    private static readonly string[] HostRecordedEventPrefixes = { "worker.", "runner." };

    // Kestrel adds Date unconditionally; Server is disabled at the listener but excluded here so
    // a configuration slip reports as an extra header rather than a silent pass. X-Powered-By is
    // IIS's, and the oracle column is Framework with no IIS in front of it.
    private static readonly HashSet<string> TransportHeaders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Date", "Server", "X-Powered-By" };

    public static void VerifySessions(
        IReadOnlyList<SessionObservation> expected,
        IReadOnlyList<SessionObservation> actual,
        TraceComparison comparison)
    {
        if (comparison == TraceComparison.Strict)
        {
            VerifyStrictSessions(expected, actual);
        }
        else
        {
            VerifyAdapterSessions(expected, actual);
        }
    }

    private static void VerifyStrictSessions(
        IReadOnlyList<SessionObservation> expected,
        IReadOnlyList<SessionObservation> actual)
    {
        VerifyValue("$.Sessions.Count", expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            var path = "$.Sessions[" + expected[index].Name + "]";
            VerifyValue(path + ".Name", expected[index].Name, actual[index].Name);
            VerifyList(
                path + ".ApplicationEvents",
                expected[index].ApplicationEvents,
                actual[index].ApplicationEvents,
                VerifyValue);
            VerifyList(
                path + ".SessionEvents",
                expected[index].SessionEvents,
                actual[index].SessionEvents,
                VerifyValue);
            VerifyStrictRequests(path, expected[index].Requests, actual[index].Requests);
        }
    }

    private static void VerifyStrictRequests(
        string sessionPath,
        IReadOnlyList<RequestObservation> expected,
        IReadOnlyList<RequestObservation> actual)
    {
        VerifyValue(sessionPath + ".Requests.Count", expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            var path = sessionPath + ".Requests[" + expected[index].Name + "]";
            VerifyValue(path + ".Name", expected[index].Name, actual[index].Name);
            VerifyStrictObservation(path, expected[index].Observation, actual[index].Observation);
        }
    }

    private static void VerifyStrictObservation(
        string path,
        PipelineObservation expected,
        PipelineObservation actual)
    {
        VerifyList(path + ".Events", expected.Events, actual.Events, VerifyValue);
        VerifyValue(
            path + ".Response.StatusCode",
            expected.Response.StatusCode,
            actual.Response.StatusCode);
        VerifyValue(
            path + ".Response.StatusDescription",
            expected.Response.StatusDescription,
            actual.Response.StatusDescription);
        VerifyList(
            path + ".Response.Headers",
            expected.Response.Headers,
            actual.Response.Headers,
            (headerPath, left, right) =>
            {
                VerifyValue(headerPath + ".Name", left.Name, right.Name);
                VerifyValue(headerPath + ".Value", left.Value, right.Value);
            });
        VerifyValue(
            path + ".Response.BodyBase64",
            expected.Response.BodyBase64,
            actual.Response.BodyBase64);
        VerifyList(
            path + ".Response.Flushes",
            expected.Response.Flushes,
            actual.Response.Flushes,
            VerifyValue);
        VerifyException(
            path + ".EscapedException",
            expected.EscapedException,
            actual.EscapedException);
        VerifyValue(
            path + ".EndOfRequestCount",
            expected.EndOfRequestCount,
            actual.EndOfRequestCount);
        VerifyValue(
            path + ".CompletionCount",
            expected.CompletionCount,
            actual.CompletionCount);
    }

    // The adapter observes a response after it has crossed a socket, so it can only compare what
    // survives the transport; every value the skipped worker.*/runner.* events announce is
    // compared as a field anyway.
    private static void VerifyAdapterSessions(
        IReadOnlyList<SessionObservation> expected,
        IReadOnlyList<SessionObservation> actual)
    {
        VerifyValue("$.Sessions.Count", expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            VerifyAdapterSession(expected[index], actual[index]);
        }
    }

    private static void VerifyAdapterSession(
        SessionObservation expected,
        SessionObservation actual)
    {
        var path = "$.Sessions['" + expected.Name + "']";

        VerifyValue("$.Sessions.Name", expected.Name, actual.Name);
        VerifySequence(
            path + ".ApplicationEvents",
            Sorted(expected.ApplicationEvents),
            Sorted(actual.ApplicationEvents));
        VerifySequence(path + ".SessionEvents", expected.SessionEvents, actual.SessionEvents);
        VerifyValue(path + ".Requests.Count", expected.Requests.Count, actual.Requests.Count);

        foreach (var expectedRequest in expected.Requests)
        {
            var actualRequest = actual.Requests.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.Name,
                        expectedRequest.Name,
                        StringComparison.Ordinal))
                ?? throw Mismatch(
                    path + ".Requests['" + expectedRequest.Name + "']",
                    "present",
                    "absent");

            VerifyAdapterObservation(
                path + ".Requests['" + expectedRequest.Name + "']",
                expectedRequest.Observation,
                actualRequest.Observation);
        }
    }

    private static void VerifyAdapterObservation(
        string path,
        PipelineObservation expected,
        PipelineObservation actual)
    {
        VerifySequence(
            path + ".Events",
            expected.Events.Where(IsPipelineEvent).ToList(),
            actual.Events);
        VerifyValue(
            path + ".Response.StatusCode",
            expected.Response.StatusCode,
            actual.Response.StatusCode);
        VerifyValue(
            path + ".Response.StatusDescription",
            expected.Response.StatusDescription,
            actual.Response.StatusDescription);
        VerifySequence(
            path + ".Response.Headers",
            Sorted(expected.Response.Headers.Select(Format)),
            Sorted(actual.Response.Headers.Where(IsApplicationHeader).Select(Format)));
        VerifyValue(
            path + ".Response.BodyBase64",
            expected.Response.BodyBase64,
            actual.Response.BodyBase64);
    }

    private static bool IsPipelineEvent(string value)
    {
        return !HostRecordedEventPrefixes.Any(
            prefix => value.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsApplicationHeader(HeaderObservation header)
    {
        return !TransportHeaders.Contains(header.Name);
    }

    private static string Format(HeaderObservation header)
    {
        return header.Name + ": " + header.Value;
    }

    private static List<string> Sorted(IEnumerable<string> values)
    {
        var sorted = values.ToList();
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }

    private static void VerifySequence(
        string path,
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual)
    {
        if (expected.Count != actual.Count)
        {
            throw Mismatch(path, expected, actual);
        }

        for (var index = 0; index < expected.Count; index++)
        {
            if (!string.Equals(expected[index], actual[index], StringComparison.Ordinal))
            {
                throw Mismatch(path + "[" + index + "]", expected[index], actual[index]);
            }
        }
    }

    private static void VerifyList<T>(
        string path,
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        Action<string, T, T> verifyItem)
    {
        VerifyValue(path + ".Count", expected.Count, actual.Count);

        for (var index = 0; index < expected.Count; index++)
        {
            verifyItem(path + "[" + index + "]", expected[index], actual[index]);
        }
    }

    private static void VerifyException(
        string path,
        ExceptionObservation? expected,
        ExceptionObservation? actual)
    {
        if (expected == null || actual == null)
        {
            if (expected != actual)
            {
                throw Mismatch(path, expected, actual);
            }

            return;
        }

        VerifyValue(path + ".Type", expected.Type, actual.Type);
        VerifyValue(path + ".Message", expected.Message, actual.Message);
        VerifyValue(path + ".HResult", expected.HResult, actual.HResult);
        VerifyException(path + ".Inner", expected.Inner, actual.Inner);
    }

    private static void VerifyValue<T>(string path, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw Mismatch(path, expected, actual);
        }
    }

    private static InvalidOperationException Mismatch(
        string path,
        object? expected,
        object? actual)
    {
        return new InvalidOperationException(
            "Parity mismatch at "
            + path
            + ": expected "
            + ParityJson.Serialize(expected)
            + ", actual "
            + ParityJson.Serialize(actual)
            + ".");
    }
}
