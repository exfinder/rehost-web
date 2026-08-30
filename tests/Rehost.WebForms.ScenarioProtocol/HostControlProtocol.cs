namespace Rehost.WebForms.ScenarioProtocol;

// Stop over HTTP because no portable API sends SIGTERM; same host-lifetime path.
public static class HostControlProtocol
{
    public const string StopPath = "/host-stop";
}
