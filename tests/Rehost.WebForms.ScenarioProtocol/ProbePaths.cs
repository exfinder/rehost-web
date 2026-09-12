namespace Rehost.WebForms.ScenarioProtocol;

// Request paths of the scenario probe handlers, as tests spell them. Each fixture web.config
// registers the same path without the leading slash; ProbePathConformanceTests holds the two
// spellings together, so a rename that touches only one side fails there.
public static class ProbePaths
{
    public const string Witness = "/witness";
    public const string Headers = "/headers";
    public const string CustomHeaders = "/custom-headers";
    public const string RequestHeaders = "/request-headers";
    public const string OnSendingHeaders = "/on-sending-headers";
    public const string Body = "/body";
    public const string Cookies = "/cookies";
    public const string Save = "/save";
    public const string WebSocketEcho = "/ws/echo";
    public const string StreamFlush = "/stream/flush";
    public const string StreamAsyncFlush = "/stream/asyncflush";
    public const string Session = "/session";
    public const string SessionPlain = "/session-plain";
    public const string SessionReadOnly = "/session-readonly";
    public const string Quirks = "/quirks";
    public const string AuthStatus = "/auth-status";
    public const string AuthSignIn = "/auth-signin";
    public const string AuthProfile = "/auth-profile";
    public const string Secret = "/Secret/secret";
    public const string ScenarioDefault = "/default";
    public const string ModuleList = "/module-list.axd";
    public const string RuntimeIdentity = "/runtime-identity";
    public const string Unload = "/unload";
    public const string Lifecycle = "/lifecycle";
    public const string Integrated = "/integrated";
    public const string Disconnect = "/disconnect";
}
