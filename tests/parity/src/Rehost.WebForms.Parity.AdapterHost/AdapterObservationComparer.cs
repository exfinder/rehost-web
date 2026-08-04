using System;
using System.Collections.Generic;
using System.Linq;
using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.AdapterHost;

// The adapter observes a response after it has crossed a socket, so it can only compare what
// survives the transport. Events named worker.* and runner.* are raised where the bench records
// its own calls into System.Web; the adapter never sees those moments, and every value they
// announce is compared as a field anyway.
internal static class AdapterObservationComparer
{
    private static readonly string[] HostRecordedEventPrefixes = { "worker.", "runner." };

    // Kestrel adds Date unconditionally; Server is disabled at the listener but excluded here so
    // a configuration slip reports as an extra header rather than a silent pass.
    private static readonly HashSet<string> TransportHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Date", "Server" };

    internal static void VerifySessions(
        List<SessionObservation> expected,
        List<SessionObservation> actual)
    {
        if (expected.Count != actual.Count)
        {
            throw Mismatch("$.Sessions.Count", expected.Count, actual.Count);
        }

        for (var index = 0; index < expected.Count; index++)
        {
            VerifySession(expected[index], actual[index]);
        }
    }

    private static void VerifySession(SessionObservation expected, SessionObservation actual)
    {
        var path = "$.Sessions['" + expected.Name + "']";

        if (!string.Equals(expected.Name, actual.Name, StringComparison.Ordinal))
        {
            throw Mismatch("$.Sessions.Name", expected.Name, actual.Name);
        }

        VerifySequence(
            path + ".ApplicationEvents",
            Sorted(expected.ApplicationEvents),
            Sorted(actual.ApplicationEvents));
        VerifySequence(path + ".SessionEvents", expected.SessionEvents, actual.SessionEvents);

        if (expected.Requests.Count != actual.Requests.Count)
        {
            throw Mismatch(
                path + ".Requests.Count",
                expected.Requests.Count,
                actual.Requests.Count);
        }

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

            VerifyRequest(
                path + ".Requests['" + expectedRequest.Name + "']",
                expectedRequest.Observation,
                actualRequest.Observation);
        }
    }

    private static void VerifyRequest(
        string path,
        PipelineObservation expected,
        PipelineObservation actual)
    {
        VerifySequence(
            path + ".Events",
            expected.Events.Where(IsPipelineEvent).ToList(),
            actual.Events);

        if (expected.Response.StatusCode != actual.Response.StatusCode)
        {
            throw Mismatch(
                path + ".Response.StatusCode",
                expected.Response.StatusCode,
                actual.Response.StatusCode);
        }

        if (!string.Equals(
                expected.Response.StatusDescription,
                actual.Response.StatusDescription,
                StringComparison.Ordinal))
        {
            throw Mismatch(
                path + ".Response.StatusDescription",
                expected.Response.StatusDescription,
                actual.Response.StatusDescription);
        }

        VerifySequence(
            path + ".Response.Headers",
            Sorted(expected.Response.Headers.Select(Format)),
            Sorted(actual.Response.Headers.Where(IsApplicationHeader).Select(Format)));

        if (!string.Equals(
                expected.Response.BodyBase64,
                actual.Response.BodyBase64,
                StringComparison.Ordinal))
        {
            throw Mismatch(
                path + ".Response.BodyBase64",
                expected.Response.BodyBase64,
                actual.Response.BodyBase64);
        }
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

    private static void VerifySequence(string path, List<string> expected, List<string> actual)
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

    private static InvalidOperationException Mismatch(
        string path,
        object? expected,
        object? actual)
    {
        return new InvalidOperationException(
            "Adapter parity mismatch at "
            + path
            + ": expected "
            + System.Text.Json.JsonSerializer.Serialize(expected)
            + ", actual "
            + System.Text.Json.JsonSerializer.Serialize(actual)
            + ".");
    }
}
