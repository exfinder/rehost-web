#nullable enable

using System.Security.Principal;
using System.Threading;
using System.Web.Util;

namespace System.Web;

internal static class ThreadPrincipalReader
{
    private static int _warned;

    internal static IPrincipal? ReadCurrentPrincipal()
    {
        try
        {
            return Thread.CurrentPrincipal;
        }
        catch (PlatformNotSupportedException ex)
        {
            if (Interlocked.Exchange(ref _warned, 1) == 0)
            {
                RehostWebLogger.Logger.ThreadPrincipalUnreadable(ex.Message);
            }

            return null;
        }
    }
}
