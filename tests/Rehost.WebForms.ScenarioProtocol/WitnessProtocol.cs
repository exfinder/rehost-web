namespace Rehost.WebForms.ScenarioProtocol;

// Grammar of the live witness channel: the query key that tags a request with its test's
// token, and the event prefixes the witness readers select by.
public static class WitnessProtocol
{
    public const string TokenKey = "wt";
    public const string StagePrefix = "stage:";
    public const string InitNotificationPrefix = "init-notification:";
    public const string HandlerEntered = "handler-entered:";
    public const string BodyAbort = "body-abort:";
    public const string SessionEntered = "session-entered:";
    public const string SessionExited = "session-exited:";
    public const string SessionEnded = "session-ended:";
    public const string SessionStoreCall = "session-store:";
}
