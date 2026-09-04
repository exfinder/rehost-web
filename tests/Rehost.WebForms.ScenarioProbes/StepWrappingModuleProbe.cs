using System.Web;
using Rehost.WebForms.ScenarioProtocol;

namespace Rehost.WebForms.ScenarioProbes;

// OnExecuteRequestStep was refused off integrated, and an APM agent registers it from Init, so a
// refusal takes the whole host down rather than failing one request. The count is staged only
// under the flag: the modules fixture's order tests assert whole stage sequences.
public sealed class StepWrappingModuleProbe : IHttpModule
{
    private const string CountKey = "steps-wrapped";

    private static string NotificationAtInit(HttpApplication application)
    {
        try
        {
            return application.Context.CurrentNotification + "/" + application.Context.IsPostNotification;
        }
        catch (Exception refusal)
        {
            return refusal.GetType().Name;
        }
    }

    public void Init(HttpApplication application)
    {
        Witness.Record(WitnessProtocol.InitNotificationPrefix + NotificationAtInit(application));
        application.OnExecuteRequestStep((context, next) =>
        {
            context.Items[CountKey] = (context.Items[CountKey] as int? ?? 0) + 1;
            next();
        });

        application.EndRequest += (sender, _) =>
        {
            var context = ((HttpApplication)sender!).Context;
            if (context.Request.QueryString["wrap"] == "1")
            {
                Witness.Stage(context.Request, "wrapped:" + context.Items[CountKey]);
            }
        };
    }

    public void Dispose()
    {
    }
}
