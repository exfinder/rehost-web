namespace Rehost.Web.AspNetCore.Tests;

using System;
using System.Text;

internal static class RawResponse
{
    internal static (string Headers, string Body) Split(byte[] raw)
    {
        var text = Encoding.Latin1.GetString(raw);
        var boundary = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        return (text[..boundary], text[(boundary + 4)..]);
    }
}
