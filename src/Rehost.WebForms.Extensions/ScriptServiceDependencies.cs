// The members of Script/Services the frozen-template closure reaches, mirrored because their own
// files pull the WCF-hosted application services. Nothing here serves the .axd names it renders.
using System;

namespace System.Web.Script.Services;

internal static class RestHandlerFactory
{
    internal const string ClientProxyRequestPathInfo = "/js";
    internal const string ClientDebugProxyRequestPathInfo = "/jsdebug";

    internal static bool IsRestMethodCall(HttpRequest request)
    {
        return !string.IsNullOrEmpty(request.PathInfo) &&
            (request.ContentType.StartsWith("application/json;", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase));
    }
}

internal static class WebServiceData
{
    internal const string _profileServiceFileName = "Profile_JSON_AppService.axd";
    internal const string _authenticationServiceFileName = "Authentication_JSON_AppService.axd";
    internal const string _roleServiceFileName = "Role_JSON_AppService.axd";
}
