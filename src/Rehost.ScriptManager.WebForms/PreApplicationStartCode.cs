using System.ComponentModel;
using System.Web;
using System.Web.UI;

[assembly: PreApplicationStartMethod(
    typeof(Rehost.ScriptManager.WebForms.PreApplicationStartCode),
    nameof(Rehost.ScriptManager.WebForms.PreApplicationStartCode.Start))]

namespace Rehost.ScriptManager.WebForms;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class PreApplicationStartCode
{
    public static void Start()
    {
        System.Web.UI.ScriptManager.ScriptResourceMapping.AddDefinition(
            "WebFormsBundle",
            new ScriptResourceDefinition
            {
                Path = "~/bundles/WebFormsJs",
                CdnPath = "http://ajax.aspnetcdn.com/ajax/4.5.1/1/WebFormsBundle.js",
                LoadSuccessExpression = "window.WebForm_PostBackOptions",
                CdnSupportsSecureConnection = true
            });
    }
}
