using System;
using System.IO;
using System.Web;
using System.Web.Compilation;

public static class RazorProbe
{
    public static string ParseRazor(string virtualPath, object model)
    {
        Type t = BuildManager.GetCompiledType(virtualPath);
        HttpContextWrapper wrapper = new HttpContextWrapper(HttpContext.Current);
        System.Web.WebPages.WebPage webpage = (System.Web.WebPages.WebPage)Activator.CreateInstance(t);
        webpage.VirtualPath = virtualPath;
        StringWriter writer = new StringWriter();
        webpage.ExecutePageHierarchy(new System.Web.WebPages.WebPageContext(wrapper, webpage, model), writer, webpage);
        return writer.ToString();
    }

    public static string Widget(string title)
    {
        var model = new { Id = "w1", Name = "Tags", Title = title };
        return ParseRazor("~/Widgets/Tags/widget.cshtml", model);
    }
}
