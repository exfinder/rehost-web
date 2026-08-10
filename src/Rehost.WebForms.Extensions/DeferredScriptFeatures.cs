using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Web.UI;

namespace System.Web.Handlers
{
    internal static class ScriptResourceHandler
    {
        private const string ScriptResourceUrlMessage =
            "ScriptResource.axd is not supported. Give the ScriptReference a Path, "
            + "or register its name with ScriptManager.ScriptResourceMapping.";

        internal static CultureInfo DetermineNearestAvailableCulture(
            Assembly assembly,
            string scriptResourceName,
            CultureInfo culture)
        {
            throw new NotSupportedException(
                "Localized embedded script resources are not supported.");
        }

        internal static string GetEmptyPageUrl(string title)
        {
            throw new NotSupportedException(
                "ScriptManager.EnableHistory requires EmptyPageUrl to be set: "
                + "ScriptResource.axd is not supported.");
        }

        internal static string GetScriptResourceUrl(
            Assembly assembly,
            string resourceName,
            CultureInfo culture,
            bool zip)
        {
            throw new NotSupportedException(ScriptResourceUrlMessage);
        }

        internal static string GetScriptResourceUrl(
            List<Tuple<Assembly, List<Tuple<string, CultureInfo>>>> assemblyResourceLists,
            bool zip)
        {
            throw new NotSupportedException(ScriptResourceUrlMessage);
        }
    }
}

namespace System.Web.Script.Services
{
    internal static class WebServiceClientProxyGenerator
    {
        internal static string GetInlineClientProxyScript(string path, HttpContext context, bool debug)
        {
            throw new NotSupportedException(
                "ServiceReference client proxies are not supported.");
        }
    }

    internal static class PageClientProxyGenerator
    {
        internal static string GetClientProxyScript(HttpContext context, IPage page, bool debug)
        {
            throw new NotSupportedException(
                "ScriptManager.EnablePageMethods is not supported.");
        }
    }
}
