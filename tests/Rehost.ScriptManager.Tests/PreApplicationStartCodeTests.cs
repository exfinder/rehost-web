using Shouldly;
using Xunit;
using MSAjaxStartCode = Rehost.ScriptManager.MSAjax.PreApplicationStartCode;
using WebFormsStartCode = Rehost.ScriptManager.WebForms.PreApplicationStartCode;

namespace Rehost.ScriptManager.Tests;

public sealed class PreApplicationStartCodeTests
{
    private static readonly Dictionary<string, string> MicrosoftAjaxScripts = new()
    {
        ["MicrosoftAjax.js"] = "window.Sys && Sys._Application && Sys.Observer",
        ["MicrosoftAjaxCore.js"] = "window.Type && Sys.Observer",
        ["MicrosoftAjaxGlobalization.js"] = "window.Sys && Sys.CultureInfo",
        ["MicrosoftAjaxSerialization.js"] = "window.Sys && Sys.Serialization",
        ["MicrosoftAjaxComponentModel.js"] = "window.Sys && Sys.CommandEventArgs",
        ["MicrosoftAjaxNetwork.js"] = "window.Sys && Sys.Net && Sys.Net.WebRequestExecutor",
        ["MicrosoftAjaxHistory.js"] = "window.Sys && Sys.HistoryEventArgs",
        ["MicrosoftAjaxWebServices.js"] = "window.Sys && Sys.Net && Sys.Net.WebServiceProxy",
        ["MicrosoftAjaxTimer.js"] = "window.Sys && Sys.UI && Sys.UI._Timer",
        ["MicrosoftAjaxWebForms.js"] = "window.Sys && Sys.WebForms",
        ["MicrosoftAjaxApplicationServices.js"] = "window.Sys && Sys.Services"
    };

    [Fact]
    public void MSAjaxRegistersMsAjaxBundleAndTheMicrosoftAjaxScripts()
    {
        System.Web.UI.ScriptManager.ScriptResourceMapping.Clear();

        MSAjaxStartCode.Start();

        var msAjax = System.Web.UI.ScriptManager.ScriptResourceMapping.GetDefinition("MsAjaxBundle");
        msAjax.ShouldNotBeNull();
        msAjax.Path.ShouldBe("~/bundles/MsAjaxJs");
        msAjax.CdnPath.ShouldBe("http://ajax.aspnetcdn.com/ajax/4.5.1/1/MsAjaxBundle.js");
        msAjax.LoadSuccessExpression.ShouldBe("window.Sys");
        msAjax.CdnSupportsSecureConnection.ShouldBeTrue();

        foreach (var (name, loadSuccessExpression) in MicrosoftAjaxScripts)
        {
            var definition = System.Web.UI.ScriptManager.ScriptResourceMapping.GetDefinition(name);
            definition.ShouldNotBeNull();
            // Microsoft.ScriptManager.MSAjax registers this casing while shipping the files under
            // MSAjax/; IIS folded the difference and ledger P57 now does.
            definition.Path.ShouldBe($"~/Scripts/WebForms/MsAjax/{name}");
            definition.CdnPath.ShouldBe($"http://ajax.aspnetcdn.com/ajax/4.5.1/1/{name}");
            definition.LoadSuccessExpression.ShouldBe(loadSuccessExpression);
            definition.CdnSupportsSecureConnection.ShouldBeTrue();
        }

        System.Web.UI.ScriptManager.ScriptResourceMapping.GetDefinition("WebFormsBundle").ShouldBeNull();
    }

    [Fact]
    public void WebFormsRegistersOnlyWebFormsBundle()
    {
        System.Web.UI.ScriptManager.ScriptResourceMapping.Clear();

        WebFormsStartCode.Start();

        var webForms = System.Web.UI.ScriptManager.ScriptResourceMapping.GetDefinition("WebFormsBundle");
        webForms.ShouldNotBeNull();
        webForms.Path.ShouldBe("~/bundles/WebFormsJs");
        webForms.CdnPath.ShouldBe("http://ajax.aspnetcdn.com/ajax/4.5.1/1/WebFormsBundle.js");
        webForms.LoadSuccessExpression.ShouldBe("window.WebForm_PostBackOptions");
        webForms.CdnSupportsSecureConnection.ShouldBeTrue();

        System.Web.UI.ScriptManager.ScriptResourceMapping.GetDefinition("MsAjaxBundle").ShouldBeNull();
        MicrosoftAjaxScripts.Keys.ShouldAllBe(
            name => System.Web.UI.ScriptManager.ScriptResourceMapping.GetDefinition(name) == null);
    }
}
