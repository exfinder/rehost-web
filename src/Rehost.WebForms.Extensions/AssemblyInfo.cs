using System.Web.Script;
using System.Web.UI;
using WRA = System.Web.UI.WebResourceAttribute;

[assembly: AjaxFrameworkAssembly]
[assembly: TagPrefix("System.Web.UI", "asp")]
[assembly: TagPrefix("System.Web.UI.WebControls", "asp")]
[assembly: System.Drawing.BitmapSuffixInSatelliteAssembly]

[assembly: ScriptResource("MicrosoftAjax.js", "System.Web.Resources.ScriptLibrary.Res", "Sys.Res")]
[assembly: ScriptResource("MicrosoftAjaxCore.js", "System.Web.Resources.ScriptLibrary.Res", "Sys.Res")]
[assembly: ScriptResource("MicrosoftAjaxWebForms.js", "System.Web.Resources.ScriptLibrary.WebForms.Res", "Sys.WebForms.Res")]

[assembly: WebResource("MicrosoftAjax.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjax.js", LoadSuccessExpression = "window.Sys && Sys._Application && Sys.Observer")]
[assembly: WebResource("MicrosoftAjaxApplicationServices.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxApplicationServices.js", LoadSuccessExpression = "window.Sys && Sys.Services")]
[assembly: WebResource("MicrosoftAjaxComponentModel.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxComponentModel.js", LoadSuccessExpression = "window.Sys && Sys.CommandEventArgs")]
[assembly: WebResource("MicrosoftAjaxCore.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxCore.js", LoadSuccessExpression = "window.Type && Sys.Observer")]
[assembly: WebResource("MicrosoftAjaxGlobalization.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxGlobalization.js", LoadSuccessExpression = "window.Sys && Sys.CultureInfo")]
[assembly: WebResource("MicrosoftAjaxHistory.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxHistory.js", LoadSuccessExpression="window.Sys && Sys.HistoryEventArgs")]
[assembly: WebResource("MicrosoftAjaxNetwork.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxNetwork.js", LoadSuccessExpression = "window.Sys && Sys.Net && Sys.Net.WebRequestExecutor")]
[assembly: WebResource("MicrosoftAjaxSerialization.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxSerialization.js", LoadSuccessExpression="window.Sys && Sys.Serialization")]
[assembly: WebResource("MicrosoftAjaxTimer.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxTimer.js", LoadSuccessExpression = "window.Sys && Sys.UI && Sys.UI._Timer")]
[assembly: WebResource("MicrosoftAjaxWebForms.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxWebForms.js", LoadSuccessExpression = "window.Sys && Sys.WebForms")]
[assembly: WebResource("MicrosoftAjaxWebServices.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "MicrosoftAjaxWebServices.js", LoadSuccessExpression = "window.Sys && Sys.Net && Sys.Net.WebServiceProxy")]
[assembly: WebResource("Date.HijriCalendar.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "Date.HijriCalendar.js", LoadSuccessExpression = "window.Type && Type._registerScript && Type._registerScript._scripts && Type._registerScript._scripts['Date.HijriCalendar.js']")]
[assembly: WebResource("Date.UmAlQuraCalendar.js", "application/x-javascript", CdnSupportsSecureConnection = true,
    CdnPath = WRA._microsoftCdnBasePath + "Date.UmAlQuraCalendar.js", LoadSuccessExpression = "window.Type && Type._registerScript && Type._registerScript._scripts && Type._registerScript._scripts['Date.UmAlQuraCalendar.js']")]
