namespace Rehost.WebForms.Parity.Harness;

public enum TraceComparison
{
    // Every recorded field, ordered, byte-for-byte values.
    Strict,

    // What survives the transport: header bag without Date/Server, pipeline events without the
    // worker.*/runner.* moments the bench records around its own calls into System.Web.
    Adapter
}
