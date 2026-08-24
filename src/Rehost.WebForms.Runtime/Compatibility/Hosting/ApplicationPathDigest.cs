namespace Rehost.WebForms.Hosting;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class ApplicationPathDigest
{
    internal static string Segment(string physicalApplicationRoot, int byteCount)
    {
        var normalized = Path.TrimEndingDirectorySeparator(physicalApplicationRoot ?? String.Empty);
        if (OperatingSystem.IsWindows())
        {
            normalized = normalized.ToLowerInvariant();
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(digest.AsSpan(0, byteCount));
    }
}
