using System;
using System.Web.Configuration;

namespace System.Web.Compilation;

internal static class BrowserCapabilitiesCompiler
{
    private const string UnsupportedMessage =
        "Application-level browser capability compilation from App_Browsers is not supported by Rehost.WebForms.";

    private static readonly BrowserCapabilitiesFactoryBase Factory = new BrowserCapabilitiesFactory();

    internal static BrowserCapabilitiesFactoryBase BrowserCapabilitiesFactory
    {
        get
        {
            ThrowIfApplicationBrowsersExist();
            return Factory;
        }
    }

    internal static Type GetBrowserCapabilitiesType()
    {
        ThrowIfApplicationBrowsersExist();
        return typeof(BrowserCapabilitiesFactory);
    }

    private static void ThrowIfApplicationBrowsersExist()
    {
        var applicationBrowsersPath = HttpRuntime.AppDomainAppVirtualPathObject
            .SimpleCombineWithDir(HttpRuntime.BrowsersDirectoryName);
        if (applicationBrowsersPath.DirectoryExists())
        {
            throw new PlatformNotSupportedException(UnsupportedMessage);
        }
    }
}
