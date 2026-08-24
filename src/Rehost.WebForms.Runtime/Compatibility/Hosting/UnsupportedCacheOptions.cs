#nullable enable

namespace System.Web.Hosting;

using System.Configuration;

internal static class UnsupportedCacheOptions
{
    internal const string PrivateBytesLimitMessage =
        """
        <cache privateBytesLimit> is not supported: this runtime cannot measure cache size, because
        System.SizedReference has no .NET equivalent. Remove the setting, or bound total process
        memory with <processModel memoryLimit>.
        """;

    internal static void RejectPrivateBytesLimit(long privateBytesLimit)
    {
        if (privateBytesLimit != 0)
        {
            throw new ConfigurationErrorsException(PrivateBytesLimitMessage);
        }
    }
}
