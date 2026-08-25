using System.ComponentModel;
using System.Web;
using System.Web.UI;

[assembly: PreApplicationStartMethod(
    typeof(eShopLegacyWebForms.PreApplicationStartCode),
    nameof(eShopLegacyWebForms.PreApplicationStartCode.Start))]

namespace eShopLegacyWebForms;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class PreApplicationStartCode
{
    public static void Start()
    {
        System.Web.UI.ScriptManager.ScriptResourceMapping.AddDefinition(
            "jquery",
            new ScriptResourceDefinition
            {
                Path = "~/Scripts/jquery-3.3.1.min.js",
                DebugPath = "~/Scripts/jquery-3.3.1.js",
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
