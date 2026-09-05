namespace Rehost.WebForms.ScenarioProtocol;

// The stop signals arrive while the process is ending, so nothing can read them back over HTTP.
// They go to a file outside the application named by the environment, like the app-start fault
// marker.
public static class ShutdownProtocol
{
    public const string LogVariable = "REHOST_SCENARIO_SHUTDOWN_LOG";

    public const string ThrowingSubscriberVariable = "REHOST_SCENARIO_STOPLISTENING_THROWS";

    public const string StopListeningEvent = "stop-listening:event";

    public const string StopListeningThrew = "stop-listening:threw";

    public const string StopListeningObject = "stop-listening:object";

    public const string RegisteredStop = "registered-stop:";

    public const string RecycleRequested = "recycle-requested";
}
