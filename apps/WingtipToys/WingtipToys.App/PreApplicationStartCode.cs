using System.ComponentModel;
using System.Web;
using System.Web.UI;

[assembly: PreApplicationStartMethod(
    typeof(WingtipToys.PreApplicationStartCode),
    nameof(WingtipToys.PreApplicationStartCode.Start))]

namespace WingtipToys;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class PreApplicationStartCode
{
    public static void Start()
    {
        System.Web.UI.ScriptManager.ScriptResourceMapping.AddDefinition(
            "jquery",
            new ScriptResourceDefinition
            {
                Path = "~/Scripts/jquery-1.10.2.min.js",
                DebugPath = "~/Scripts/jquery-1.10.2.js",
                LoadSuccessExpression = "window.jQuery"
            });
        System.Web.UI.ScriptManager.ScriptResourceMapping.AddDefinition(
            "bootstrap",
            new ScriptResourceDefinition
            {
                Path = "~/Scripts/bootstrap.min.js",
                DebugPath = "~/Scripts/bootstrap.js"
            });
    }
}
