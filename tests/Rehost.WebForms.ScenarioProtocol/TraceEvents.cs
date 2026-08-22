namespace Rehost.WebForms.ScenarioProtocol;

// Line prefixes of the trace file, shared by writers (host, staged probes, fixture
// App_Code) and the readers that select by prefix. Complete expected lines stay literal
// in tests: the literal is the assertion.
public static class TraceEvents
{
    public const string Address = "address:";
    public const string Request = "request:";
    public const string ContentType = "content-type:";
    public const string ErrorBody = "error-body:";
    public const string AppCode = "app-code:";
    public const string SubCode = "sub-code:";
    public const string Resource = "resource:";
}
