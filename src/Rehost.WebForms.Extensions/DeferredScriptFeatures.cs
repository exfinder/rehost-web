using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Web.UI;

namespace System.Web.Handlers
{
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
