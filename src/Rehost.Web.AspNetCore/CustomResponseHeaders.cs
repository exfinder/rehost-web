namespace Rehost.Web.AspNetCore;

using System;
using System.Threading.Tasks;
using System.Web.IisConfig;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

// IIS's protocol module wrote the configured rows as the head left, after managed code was done
// with the response, which is why an application's Headers.Remove and ClearHeaders never reached
// one.
internal static class CustomResponseHeaders
{
    internal static void Register(HttpContext context, CustomHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(headers);

        context.Response.OnStarting(() =>
        {
            Apply(context.Response.Headers, headers);
            return Task.CompletedTask;
        });
    }

    internal static void Apply(IHeaderDictionary target, CustomHeaders headers)
    {
        foreach (var row in headers.Rows)
        {
            if (row.Value.Length == 0)
            {
                continue;
            }

            var existing = target[row.Name];
            if (Coalesces(row.Name) && !StringValues.IsNullOrEmpty(existing))
            {
                target[row.Name] = $"{existing},{row.Value}";
            }
            else
            {
                target.Append(row.Name, row.Value);
            }
        }
    }

    private static bool Coalesces(string name) =>
        string.Equals(name, "Cache-Control", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase);
}
