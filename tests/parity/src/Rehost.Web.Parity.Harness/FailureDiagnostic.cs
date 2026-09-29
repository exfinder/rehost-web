using Rehost.Web.Parity.Contracts;

namespace Rehost.Web.Parity.Harness;

public sealed class FailureDiagnostic
{
    public int SchemaVersion { get; set; }

    public string Phase { get; set; } = "";

    public ExceptionObservation Exception { get; set; } = new ExceptionObservation();
}
