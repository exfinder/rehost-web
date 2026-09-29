namespace Rehost.Web.Hosting;

using System;
using System.Web;

internal static class HostDirectory
{
    internal static string Path { get; } =
        System.IO.Path.GetDirectoryName(typeof(HttpRuntime).Assembly.Location);

    internal static void PublishBaseDirectory(string physicalRootPath)
    {
        AppContext.SetData(
            "APP_CONTEXT_BASE_DIRECTORY",
            System.IO.Path.EndsInDirectorySeparator(physicalRootPath)
                ? physicalRootPath
                : $"{physicalRootPath}{System.IO.Path.DirectorySeparatorChar}");
    }
}
