namespace Rehost.WebForms.Hosting;

using System;
using System.Collections.Generic;
using System.Text;

// http.sys canonicalized the URL before IIS or ASP.NET saw it: '\' became '/', %2F a real
// separator, repeated separators one, and '.'/'..' segments were resolved, refusing the URL
// (403) when '..' climbed above the root. Kestrel leaves %2F encoded and passes an escaping
// '..' through, so the adapter finishes the job here (IIS reading, ledger P72).
internal static class RequestPathCanonicalizer
{
    // The raw request target, still percent-encoded and with its query.
    internal static bool EscapesRoot(string? rawTarget)
    {
        if (string.IsNullOrEmpty(rawTarget) || rawTarget[0] != '/')
        {
            return false;
        }

        var query = rawTarget.IndexOf('?');
        var path = query < 0 ? rawTarget : rawTarget[..query];
        try
        {
            path = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            return false;
        }

        Canonicalize(path, out var escapesRoot);
        return escapesRoot;
    }

    internal static string Canonicalize(string path, out bool escapesRoot)
    {
        escapesRoot = false;
        var decoded = path.Replace('\\', '/');
        if (decoded.Contains("%2F", StringComparison.OrdinalIgnoreCase))
        {
            decoded = decoded.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
        }

        var segments = decoded.Split('/');
        var output = new List<string>(segments.Length);
        var trailingSlash = false;
        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            var last = i == segments.Length - 1;
            if (segment.Length == 0 || segment == ".")
            {
                trailingSlash = last;
                continue;
            }

            if (segment == "..")
            {
                if (output.Count == 0)
                {
                    escapesRoot = true;
                }
                else
                {
                    output.RemoveAt(output.Count - 1);
                }

                trailingSlash = last;
                continue;
            }

            output.Add(segment);
            trailingSlash = false;
        }

        var result = new StringBuilder(decoded.Length);
        foreach (var segment in output)
        {
            result.Append('/').Append(segment);
        }

        if (output.Count == 0 || trailingSlash)
        {
            result.Append('/');
        }

        return result.ToString();
    }
}
