using System.ComponentModel;
using System.Web;
using System.Web.UI;

[assembly: PreApplicationStartMethod(
    typeof(Rehost.ScriptManager.MSAjax.PreApplicationStartCode),
    nameof(Rehost.ScriptManager.MSAjax.PreApplicationStartCode.Start))]

namespace Rehost.ScriptManager.MSAjax;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class PreApplicationStartCode
{
    public static void Start()
    {
        AddDefinition(
            "MsAjaxBundle",
            "~/bundles/MsAjaxJs",
            "http://ajax.aspnetcdn.com/ajax/4.5.1/1/MsAjaxBundle.js",
            "window.Sys");
        AddMsAjaxDefinition("MicrosoftAjax.js", "window.Sys && Sys._Application && Sys.Observer");
        AddMsAjaxDefinition("MicrosoftAjaxCore.js", "window.Type && Sys.Observer");
        AddMsAjaxDefinition("MicrosoftAjaxGlobalization.js", "window.Sys && Sys.CultureInfo");
        AddMsAjaxDefinition("MicrosoftAjaxSerialization.js", "window.Sys && Sys.Serialization");
        AddMsAjaxDefinition("MicrosoftAjaxComponentModel.js", "window.Sys && Sys.CommandEventArgs");
        AddMsAjaxDefinition("MicrosoftAjaxNetwork.js", "window.Sys && Sys.Net && Sys.Net.WebRequestExecutor");
        AddMsAjaxDefinition("MicrosoftAjaxHistory.js", "window.Sys && Sys.HistoryEventArgs");
        AddMsAjaxDefinition("MicrosoftAjaxWebServices.js", "window.Sys && Sys.Net && Sys.Net.WebServiceProxy");
        AddMsAjaxDefinition("MicrosoftAjaxTimer.js", "window.Sys && Sys.UI && Sys.UI._Timer");
        AddMsAjaxDefinition("MicrosoftAjaxWebForms.js", "window.Sys && Sys.WebForms");
        AddMsAjaxDefinition("MicrosoftAjaxApplicationServices.js", "window.Sys && Sys.Services");
    }

    private static void AddMsAjaxDefinition(string name, string loadSuccessExpression) =>
        AddDefinition(
            name,
            $"~/Scripts/WebForms/MsAjax/{name}",
            $"http://ajax.aspnetcdn.com/ajax/4.5.1/1/{name}",
            loadSuccessExpression);

    private static void AddDefinition(string name, string path, string cdnPath, string loadSuccessExpression)
    {
        System.Web.UI.ScriptManager.ScriptResourceMapping.AddDefinition(
            name,
            new ScriptResourceDefinition
            {
                Path = path,
                CdnPath = cdnPath,
                LoadSuccessExpression = loadSuccessExpression,
                CdnSupportsSecureConnection = true
            });
    }
}
