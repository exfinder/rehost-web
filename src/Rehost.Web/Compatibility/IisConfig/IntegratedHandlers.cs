#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Web.Util;

namespace System.Web.IisConfig;

// Handler selection over the merged system.webServer/handlers snapshot: first match in document
// order, no specificity ranking (MH8, MH9v2), a verb-mismatched row skipped rather than answered
// with 405 (MH21), and 404 when the list runs out (MH18). The list is the one resolved for the
// request path's own directory (MH27).
internal static class IntegratedHandlers
{
    // The row IIS would name in its error page, which is also the row whose managed-or-native
    // nature answers preCondition="managedHandler". A transparent TransferRequestHandler row is
    // the selection here even though dispatch walks past it.
    internal static IisHandlerRoute? Selected(string requestType, VirtualPath path) =>
        Selected(
            IisServerConfiguration.Current.HandlerRoutesFor(path), requestType, path);

    internal static IisHandlerRoute? Selected(
        IReadOnlyList<IisHandlerRoute> routes, string requestType, VirtualPath path)
    {
        foreach (var route in routes)
        {
            if (route.Matches(requestType, path))
            {
                return route;
            }
        }

        return null;
    }

    internal static IisHandlerRoute? Resolve(
        IReadOnlyList<IisHandlerRoute> routes,
        string requestType,
        VirtualPath path,
        string? pathTranslated,
        out string refusal)
    {
        foreach (var route in routes)
        {
            if (!route.Matches(requestType, path) || route.IsTransferRequest)
            {
                continue;
            }

            if (ResourcePresent(route, pathTranslated))
            {
                refusal = "";
                return route;
            }

            refusal = "<add name=\"" + route.Registration.Name + "\"> in <system.webServer><handlers>"
                + " requires resourceType=\"" + route.ResourceType + "\" for "
                + path.VirtualPathString + ", and nothing is at " + pathTranslated + ".";
            return null;
        }

        refusal = "No <system.webServer><handlers> entry matches " + requestType + " "
            + path.VirtualPathString + ".";
        return null;
    }

    internal static IHttpHandler Map(
        HttpContext context,
        string requestType,
        VirtualPath path,
        string pathTranslated,
        Func<string, IHttpHandlerFactory> factories,
        out IHttpHandlerFactory? factory)
    {
        var route = Resolve(
            IisServerConfiguration.Current.HandlerRoutesFor(path),
            requestType,
            path,
            pathTranslated,
            out var refusal);

        if (route == null)
        {
            factory = null;
            return new NativeRefusalHandler(404, refusal);
        }

        if (route.Bridge != IisNativeBridge.None)
        {
            factory = null;
            return NativeHandler(route, requestType);
        }

        factory = ResolveFactory(route, factories);
        return CreateHandler(factory, context, requestType, path, pathTranslated);
    }

    // Default resourceType consults nothing on disk; File requires the mapped file and answers
    // 404 naming the entry, without falling through to the next row (MH26). Either and Directory
    // are unmeasured and carry their IIS-documented meaning.
    private static bool ResourcePresent(IisHandlerRoute route, string? pathTranslated)
    {
        if (route.ResourceType == IisResourceType.Unspecified
            || string.IsNullOrEmpty(pathTranslated))
        {
            return true;
        }

        return route.ResourceType switch
        {
            IisResourceType.File => FileUtil.FileExists(pathTranslated),
            IisResourceType.Directory => FileUtil.DirectoryExists(pathTranslated),
            _ => FileUtil.FileExists(pathTranslated) || FileUtil.DirectoryExists(pathTranslated),
        };
    }

    internal static IHttpHandler NativeHandler(IisHandlerRoute route, string requestType) =>
        route.Bridge == IisNativeBridge.StaticFile && IsStaticFileVerb(requestType)
            ? new StaticFileBridgeHandler()
            : new NativeRefusalHandler(405);

    private static bool IsStaticFileVerb(string requestType) =>
        string.Equals(requestType, "GET", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requestType, "HEAD", StringComparison.OrdinalIgnoreCase)
        || string.Equals(requestType, "POST", StringComparison.OrdinalIgnoreCase);

    // A row's type resolves at first match, not at activation, because App_Code compiles after
    // the configuration is published (MH22a). The failure reaches only the URLs that got here.
    private static IHttpHandlerFactory ResolveFactory(
        IisHandlerRoute route, Func<string, IHttpHandlerFactory> factories)
    {
        try
        {
            return factories(IisTypeIdentities.Retarget(route.Registration.Type!));
        }
        catch (Exception failure)
        {
            throw new HttpException(
                "<add name=\"" + route.Registration.Name + "\"> in <system.webServer><handlers>"
                + " cannot be loaded, so this URL cannot be served. Configured type: '"
                + route.Registration.Type + "'. " + failure.Message,
                failure);
        }
    }

    private static IHttpHandler CreateHandler(
        IHttpHandlerFactory factory,
        HttpContext context,
        string requestType,
        VirtualPath path,
        string pathTranslated)
    {
        try
        {
            return factory is IHttpHandlerFactory2 factory2
                ? factory2.GetHandler(context, requestType, path, pathTranslated)
                : factory.GetHandler(context, requestType, path.VirtualPathString, pathTranslated);
        }
        catch (FileNotFoundException failure)
        {
            throw new HttpException(404, null, failure);
        }
        catch (DirectoryNotFoundException failure)
        {
            throw new HttpException(404, null, failure);
        }
        catch (PathTooLongException failure)
        {
            throw new HttpException(414, null, failure);
        }
    }
}
