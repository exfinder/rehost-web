using System;
using System.Reflection;
using System.Web;
using Rehost.WebForms.ScenarioProbes;

public partial class TimeoutSweepPage : System.Web.UI.Page
{
    protected string Result = "";

    protected void Page_Load(object sender, EventArgs e)
    {
        var token = Request.QueryString["wt"];
        if (Request.QueryString["optout"] != null)
        {
            Context.ThreadAbortOnTimeout = false;
        }

        // The far-future date makes every registered request, including this one, look expired.
        var manager = typeof(HttpRuntime)
            .GetProperty("RequestTimeoutManager", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        var sweep = manager.GetType().GetMethod(
            "CancelTimedOutRequests", BindingFlags.NonPublic | BindingFlags.Instance);
        sweep.Invoke(manager, new object[] { DateTime.UtcNow.AddYears(1) });

        if (token != null)
        {
            WitnessJournal.Record("stage:" + token + ":sweep-returned");
            WitnessJournal.Record(
                "stage:" + token + ":token-canceled:" + Request.TimedOutToken.IsCancellationRequested);
        }

        Result = "sweep-page|";
    }
}
