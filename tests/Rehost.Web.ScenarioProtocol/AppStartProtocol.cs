namespace Rehost.Web.ScenarioProtocol;

// The marker file lives outside the application directory so arming and clearing it never
// touches content the application watches.
public static class AppStartProtocol
{
    public const string FaultMarkerVariable = "REHOST_SCENARIO_APPSTART_FAULT";

    public const string FaultText = "appstart-fault:";
}
