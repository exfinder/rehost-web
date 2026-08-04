using Rehost.WebForms.Parity.Contracts;

namespace Rehost.WebForms.Parity.Harness;

public sealed class FailureDiagnostic
{
    public int SchemaVersion { get; set; }

    public string Phase { get; set; } = "";

    public ExceptionObservation Exception { get; set; } = new ExceptionObservation();
}
