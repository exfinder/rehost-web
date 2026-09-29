using System;
using System.Runtime.CompilerServices;

namespace System.Web;

internal static class UnsafeBinaryFormatterModuleInitializer
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        AppContext.SetSwitch(
            "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization",
            true);
    }
}
